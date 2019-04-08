﻿using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Harmony;
using RestoreMonarchy.TeleportationPlugin.Commands;
using Rocket.API.DependencyInjection;
using Rocket.API.Eventing;
using Rocket.API.Logging;
using Rocket.API.Permissions;
using Rocket.API.Scheduling;
using Rocket.API.User;
using Rocket.Core.Eventing;
using Rocket.Core.Logging;
using Rocket.Core.Player.Events;
using Rocket.Core.Plugins;

namespace RestoreMonarchy.TeleportationPlugin
{
    public class TeleportationPlugin : Plugin<TeleportationConfiguration>
    {
        public const string HarmonyInstanceId = "com.restoremonarchy.teleportationplugin";

        private readonly IPermissionProvider permissionProvider;
        private readonly ILogger logger;
        private readonly ITaskScheduler taskScheduler;
        private HarmonyInstance harmonyInstance;

        // Only to be used by harmony patches
        internal static TeleportationPlugin Instance { get; set; }

        public List<PlayerTeleportRequest> PendingRequests { get; } = new List<PlayerTeleportRequest>();
        public TeleportationDatabase Database { get; set; }

        public TeleportationPlugin(IDependencyContainer container, IUserManager userManager, IPermissionProvider permissionProvider, ILogger logger, ITaskScheduler taskScheduler) : base(container)
        {
            this.permissionProvider = permissionProvider;
            this.logger = logger;
            this.taskScheduler = taskScheduler;

            Instance = this;
            Database = new TeleportationDatabase(userManager, permissionProvider, this);
        }

        public override Dictionary<string, string> DefaultTranslations => new Dictionary<string, string>
        {
            {"Home_NotHave", "You don't have any bed"},
            {"Home_Driving", "You can't teleport whilte driving"},
            {"Home_NotFound", "{0} could not be found"},
            {"Home_Delay", "You will be teleported to your home in {0} seconds"},
            {"Home_Success", "You have been teleported to your bed"},
            {"Home_List", "Your homes:"},
            {"Home_Exist", "You already have a bed called {0}"},
            {"Home_Rename", "Successfully renamed {0} to {1}"},
            {"TP_NoRequestFrom", "There is no requests to you"},
            {"TP_NoRequestTo", "You didn't send a request to anyone"},
            {"TP_PendingFrom", "You have pending requests from:"},
            {"TP_PendingTo", "You've sent a request to:"},
            {"TP_Self", "You can't send a request to yourself"},
            {"TP_AlreadySent", "You've already sent a request to {0}"},
            {"TP_Limit", "You have sent to many requests, you may cancel them using /tpcancel"},
            {"TP_Sent", "You sent a request to {0}"},
            {"TP_Receive", "{0} sent you request. Respond with /tpadeny or /tpaccept"},
            {"TP_Accept", "You accepted {0} request"},
            {"TP_Accepted", "{0} accepted your request"},
            {"TP_Teleport", "{0} teleported to you"},
            {"TP_Teleported", "You have been teleported to {0}"},
            {"TP_Dead", "Couldn't teleport you, because {0} is dead or left."},
            {"TP_Deny", "You denied the request from {0}"},
            {"TP_Denied", "{0} denied your request"},
            {"TP_NoDeny", "There is no request to you"},
            {"TP_Cancel", "You canceled the request to {0}"}
        };

        protected override async Task OnActivate(bool isFromReload)
        {
            EventBus.AddEventListener(this, new TeleportationEventListener(PendingRequests));

            TeleportCommands tpaCommand = new TeleportCommands(Translations, permissionProvider, taskScheduler ,this, PendingRequests);
            HomeCommands homeCommand = new HomeCommands( Translations, taskScheduler, Database, this);

            if (ConfigurationInstance.TPEnabled)
                RegisterCommands(tpaCommand);

            if (ConfigurationInstance.HomeEnabled)
                RegisterCommands(homeCommand);

            harmonyInstance = HarmonyInstance.Create(HarmonyInstanceId);
            harmonyInstance.PatchAll(Assembly.GetExecutingAssembly());

            logger.LogInformation($"{Assembly.GetExecutingAssembly().GetName().Name} has been loaded!", LogLevel.Information);
            logger.LogInformation($"Version: {Assembly.GetExecutingAssembly().GetName().Version}", LogLevel.Information);
            logger.LogInformation($"Made by MCrow", LogLevel.Information);

            string isEnabled = "disabled";

            if (ConfigurationInstance.TPEnabled)
                isEnabled = "enabled";

            logger.LogInformation($"TP: {isEnabled}", LogLevel.Information);

            if (!ConfigurationInstance.HomeEnabled)
                isEnabled = "disabled";

            logger.LogInformation($"Home: {isEnabled}", LogLevel.Information);
        }

        protected override async Task OnDeactivate()
        {
            harmonyInstance?.UnpatchAll(HarmonyInstanceId);
            harmonyInstance = null;
        }
    }

    public class TeleportationEventListener : IEventListener<PlayerDisconnectedEvent>
    {
        private readonly List<PlayerTeleportRequest> teleportationRequests;

        public TeleportationEventListener(List<PlayerTeleportRequest> teleportationRequests)
        {
            this.teleportationRequests = teleportationRequests;
        }

        [EventHandler]
        public async Task HandleEventAsync(IEventEmitter emitter, PlayerDisconnectedEvent @event)
        {
            teleportationRequests.RemoveAll(x=> x.Receiver.Id == @event.Player.User.Id);
            teleportationRequests.RemoveAll(x => x.Sender.Id == @event.Player.User.Id);
        }
    }
}