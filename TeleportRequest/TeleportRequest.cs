using Microsoft.Xna.Framework;
using System;
using System.IO;
using System.Reflection;
using System.Timers;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;

namespace TeleportRequest
{
	[ApiVersion(2, 1)]
	public class TeleportRequest : TerrariaPlugin
	{
		public override string Author
		{
			get { return "MarioE, maintained by Ryozuki"; }
		}
		public Config Config = new Config();
		public override string Description
		{
			get { return "Adds teleportation accept commands."; }
		}
		public override string Name
		{
			get { return "Teleport"; }
		}
		private Timer Timer;
		private bool[] TPAllows = new bool[256];
		private TPRequest[] TPRequests = new TPRequest[256];
		public override Version Version
		{
			get { return Assembly.GetExecutingAssembly().GetName().Version; }
		}

		public TeleportRequest(Main game)
			: base(game)
		{
			for (int i = 0; i < TPRequests.Length; i++)
				TPRequests[i] = new TPRequest();
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
				ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
				Timer.Dispose();
			}
		}
		public override void Initialize()
		{
			ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
			ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
		}

		void OnElapsed(object sender, ElapsedEventArgs e)
		{
			for (int i = 0; i < TPRequests.Length; i++)
			{
				TPRequest tpr = TPRequests[i];
				if (tpr.timeout > 0)
				{
					TSPlayer dst = TShock.Players[tpr.dst];
					TSPlayer src = TShock.Players[i];

					tpr.timeout--;
					if (tpr.timeout == 0)
					{
						src.SendMessage("[Teleport Request] Your teleport request timed out.", Color.LightBlue);
						dst.SendMessage("[Teleport Request] " + src.Name.Colorize(Color.Yellow) + "'s teleport request timed out.", Color.LightBlue);
					}
					else
					{
						string msg = String.Format("[Teleport Request] " + src.Name.Colorize(Color.Yellow) + " is requesting to teleport to you. ({0}tpr yes or {0}tpr no)", Commands.Specifier);
						if (tpr.dir)
							msg = String.Format("[Teleport Request] You are requested to teleport to " + src.Name.Colorize(Color.Yellow) + ". ({0}tpr yes or {0}tpr no)", Commands.Specifier);
						dst.SendMessage(msg, Color.LightBlue);
					}
				}
			}
		}
		void OnInitialize(EventArgs e)
		{
			Commands.ChatCommands.Add(new Command("tprequest.use", TPA, "tpr")
			{
				AllowServer = false,
				HelpText = "Sends a request to teleport to someone."
			});

			if (File.Exists(Path.Combine(TShock.SavePath, "tpconfig.json")))
				Config = Config.Read(Path.Combine(TShock.SavePath, "tpconfig.json"));
			Config.Write(Path.Combine(TShock.SavePath, "tpconfig.json"));
			Timer = new Timer(Config.Interval * 1000);
			Timer.Elapsed += OnElapsed;
			Timer.Start();
		}
		void OnLeave(LeaveEventArgs e)
		{
			TPAllows[e.Who] = false;
			TPRequests[e.Who].timeout = 0;
		}

        void TPA(CommandArgs e)
        {
            if (e.Parameters.Count == 0)
            {
                e.Player.SendMessage("[Teleport Request]", Color.LightBlue);
                e.Player.SendInfoMessage("- Teleport: ".Colorize(Color.LightBlue) + "{0}tpr <player>", Commands.Specifier);
                if (e.Player.HasPermission("tprequest.here"))
                    e.Player.SendInfoMessage("- Teleport here: ".Colorize(Color.LightBlue) + "{0}tpr here <player>", Commands.Specifier);
                if (e.Player.HasPermission("tprequest.yes"))
                    e.Player.SendInfoMessage("- Accept request: ".Colorize(Color.LightBlue) + "{0}tpr y", Commands.Specifier);
                if (e.Player.HasPermission("tprequest.no"))
                    e.Player.SendInfoMessage("- Deny request: ".Colorize(Color.LightBlue) + "{0}tpr n", Commands.Specifier);
                if (e.Player.HasPermission("tprequest.autono"))
                    e.Player.SendInfoMessage("- Toggle auto-deny: ".Colorize(Color.LightBlue) + "{0}tpr autono", Commands.Specifier);
                return;
            }
            if ((e.Parameters[0] == "yes") || (e.Parameters[0] == "y") || (e.Parameters[0] == "Yes"))
            {
                if (!e.Player.HasPermission("tprequest.yes"))
                {
                    e.Player.SendInfoMessage("[Teleport Request] You don't have permission to use this command.", Commands.Specifier);
                    return;
                }
                for (int i = 0; i < TPRequests.Length; i++)
                {
                    TPRequest tpr = TPRequests[i];
                    if (tpr.timeout > 0 && tpr.dst == e.Player.Index)
                    {
                        TSPlayer plr1 = tpr.dir ? e.Player : TShock.Players[i];
                        TSPlayer plr2 = tpr.dir ? TShock.Players[i] : e.Player;
                        if (plr1.Teleport(plr2.X, plr2.Y))
                        {
                            plr1.SendMessage("[Teleport Request] Teleported to " + plr2.Name.Colorize(Color.Yellow) + ".", Color.LightBlue);
                            plr2.SendMessage("[Teleport Request] " + plr1.Name.Colorize(Color.Yellow) + " teleported to you.", Color.LightBlue);
                        }
                        tpr.timeout = 0;
                        return;
                    }
                }
                e.Player.SendInfoMessage("[Teleport Request] You have no pending teleport requests.");
                return;
            }
            if ((e.Parameters[0] == "no") || (e.Parameters[0] == "n") || (e.Parameters[0] == "No"))
            {
                if (!e.Player.HasPermission("tprequest.no"))
                {
                    e.Player.SendInfoMessage("[Teleport Request] You don't have permission to use this command.", Commands.Specifier);
                    return;
                }
                for (int i = 0; i < TPRequests.Length; i++)
                {
                    TPRequest tpr = TPRequests[i];
                    if (tpr.timeout > 0 && tpr.dst == e.Player.Index)
                    {
                        tpr.timeout = 0;
                        e.Player.SendMessage("[Teleport Request] Denied " + TShock.Players[i].Name.Colorize(Color.Yellow) + "'s" + " teleport request.", Color.LightCoral);
                        TShock.Players[i].SendMessage("[Teleport Request] " + e.Player.Name.Colorize(Color.Yellow) + " denied your teleport request.", Color.LightBlue);
                        return;
                    }
                }
                e.Player.SendInfoMessage("[Teleport Request] You have no pending teleport requests.");
                return;
            }
            if ((e.Parameters[0] == "here") || (e.Parameters[0] == "Here") || (e.Parameters[0] == "HERE"))
            {
                if (e.Parameters.Count == 1)
                {
                    e.Player.SendInfoMessage("[Teleport Request] Invalid syntax! Proper syntax: {0}tpr here <player>", Commands.Specifier);
                    return;
                }
                if (!e.Player.HasPermission("tprequest.here"))
                {
                    e.Player.SendErrorMessage("[Teleport Request] You don't have permission to use this command.", Commands.Specifier);
                    return;
                }
                string plrNamehere = String.Join(" ", e.Parameters.ToArray());
                plrNamehere = plrNamehere.Remove(0, 5);
                var playersherelist = TShock.Utils.FindPlayer(plrNamehere);
                if (playersherelist.Count == 0)
                    e.Player.SendErrorMessage("Invalid player!");
                else if (playersherelist.Count > 1)
                    e.Player.SendErrorMessage("More than one player matched!");
                else if ((!playersherelist[0].TPAllow || TPAllows[playersherelist[0].Index]) && !e.Player.Group.HasPermission(Permissions.tpoverride))
                    e.Player.SendInfoMessage("[Teleport Request] You cannot teleport {0}.", playersherelist[0].Name);
                else
                {
                    for (int i = 0; i < TPRequests.Length; i++)
                    {
                        TPRequest tpr = TPRequests[i];
                        if (tpr.timeout > 0 && tpr.dst == playersherelist[0].Index)
                        {
                            e.Player.SendInfoMessage("[Teleport Request] {0} already has a teleport request.", playersherelist[0].Name);
                            return;
                        }
                    }
                    if (!e.Player.HasPermission("tprequestadmin.bypass"))
                    {
                        foreach (var npc in Main.npc)
                        {
                            if ((npc.target == playersherelist[0].TPlayer.whoAmI) && npc.boss && npc.active)
                            {
                                e.Player.SendMessage("[Teleport Request] Player " + playersherelist[0].Name.Colorize(Color.Yellow) + " is being target by a Boss. Teleport failed.", Color.LightBlue);
                                return;
                            }
                        }
                    }
                    TPRequests[e.Player.Index].dir = true;
                    TPRequests[e.Player.Index].dst = (byte)playersherelist[0].Index;
                    TPRequests[e.Player.Index].timeout = Config.Timeout + 1;
                    e.Player.SendMessage("[Teleport Request]" + " Sent a teleport request to " + playersherelist[0].Name.Colorize(Color.Yellow) + ".", Color.LightCyan);
                }
                return;
            }
            if ((e.Parameters[0] == "autono") || (e.Parameters[0] == "Autono") || (e.Parameters[0] == "an"))
            {
                if (!e.Player.HasPermission("tprequest.autono"))
                {
                    e.Player.SendInfoMessage("[Teleport Request] You don't have permission to use this command.", Commands.Specifier);
                    return;
                }
                TPAllows[e.Player.Index] = !TPAllows[e.Player.Index];
                e.Player.SendInfoMessage("[Teleport Request]" + " {0}abled Teleport Auto-deny.".Colorize(Color.LightBlue), TPAllows[e.Player.Index] ? "En" : "Dis");
                return;
            }
            #region tp
            string plrName = String.Join(" ", e.Parameters.ToArray());
            var players = TShock.Utils.FindPlayer(plrName);
            if (players.Count == 0)
                e.Player.SendErrorMessage("Invalid player!");
            else if (players.Count > 1)
                e.Player.SendErrorMessage("More than one player matched!");
            else if ((!players[0].TPAllow || TPAllows[players[0].Index]) && !e.Player.Group.HasPermission(Permissions.tpoverride))
                e.Player.SendInfoMessage("[Teleport Request] You cannot teleport to {0}.", players[0].Name);
            else
            {
                for (int i = 0; i < TPRequests.Length; i++)
                {
                    TPRequest tpr = TPRequests[i];
                    if (tpr.timeout > 0 && tpr.dst == players[0].Index)
                    {
                        e.Player.SendInfoMessage("[Teleport Request] {0} already has a teleport request.", players[0].Name);
                        return;
                    }
                }
                if (!e.Player.HasPermission("tprequestadmin.bypass"))
                {
                    foreach (var npc in Main.npc)
                    {
                        if ((npc.target == players[0].TPlayer.whoAmI) && npc.boss && npc.active)
                        {
                            e.Player.SendMessage("[Teleport Request] Player " + players[0].Name.Colorize(Color.Yellow) + " is being target by a Boss. Teleport failed.", Color.LightBlue);
                            return;
                        }
                    }
                }
                TPRequests[e.Player.Index].dir = false;
                TPRequests[e.Player.Index].dst = (byte)players[0].Index;
                TPRequests[e.Player.Index].timeout = Config.Timeout + 1;
                e.Player.SendMessage("[Teleport Request]" + " Sent a teleport request to " + players[0].Name.Colorize(Color.Yellow) + ".", Color.LightBlue);
            }
            #endregion
        }
	}
}