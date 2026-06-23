using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using AuraTracker.controllers;
using AuraTracker.render;
using Coroutine;
using OriathHub;
using OriathHub.CoroutineEvents;
using OriathHub.Plugin;
using OriathHub.RemoteEnums;
using OriathHub.RemoteObjects.States;
using OriathHub.RemoteObjects.States.InGameStateObjects;
using OriathHub.Utils;

namespace AuraTracker
{
    public sealed class AuraTracker : PluginBase
    {
        private const string PluginVersion = "1.3.8.2";

        private AuraTrackerSettings settings = new();
        private readonly DpsTracker dpsTracker = new();
        private readonly MonsterCollector monsterCollector = new();
        private readonly PanelRenderer panelRenderer = new();
        private readonly SettingsUiRenderer settingsRenderer = new(PluginVersion);
        private ActiveCoroutine? onAreaChange;
        private Vector2? defaultLargeMapCenter;
        private bool isMenuOpen;

        private FileInfo SettingsFile => new(Path.Combine(DllDirectory, "config", "AuraTracker.settings.json"));

        public override string Name => "AuraTracker";

        public override string Description => "Displays monsters in a fixed list UI with health/ES bars, buffs, DPS tracking, and rarity-based prioritization.";

        public override string Author => "Skrip";

        public override string Version => PluginVersion;

        public override void OnEnable(bool isGameOpened)
        {
            settings = JsonHelper.CreateOrLoadJsonFile<AuraTrackerSettings>(SettingsFile);

            this.onAreaChange = CoroutineHandler.Start(OnAreaChange(), string.Empty, 0);
            this.defaultLargeMapCenter = null;
            this.isMenuOpen = false;
        }

        public override void OnDisable()
        {
            this.onAreaChange?.Cancel();
            this.onAreaChange = null;
            this.dpsTracker.Reset();
            this.defaultLargeMapCenter = null;
            this.isMenuOpen = false;
        }

        public override void SaveSettings()
        {
            JsonHelper.SaveToFile(settings, SettingsFile);
        }

        public override void DrawSettings()
        {
            this.settingsRenderer.Draw(settings);
        }

        public override void DrawUI()
        {
            if (Core.States.GameCurrentState != GameStateTypes.InGameState && Core.States.GameCurrentState != GameStateTypes.EscapeState)
            {
                return;
            }

            InGameState inGame = Core.States.InGameStateObject;
            if (!settings.DrawWhenGameInBackground && !Core.Process.Foreground)
            {
                return;
            }

            if (inGame.GameUi.IsSkillTreeOpen)
            {
                return;
            }

            if (this.IsMenuOpen(inGame))
            {
                return;
            }

            var overlaySize = new Vector2(Core.Overlay.Size.Width, Core.Overlay.Size.Height);
            var overlayCenter = overlaySize * 0.5f;

            List<MonsterCollector.MonsterSnapshot> monsters = this.monsterCollector.Collect(settings, inGame, overlayCenter);
            if (monsters.Count == 0)
            {
                return;
            }

            this.panelRenderer.Render(monsters, settings, this.dpsTracker, overlaySize);
        }

        private IEnumerator<Wait> OnAreaChange()
        {
            for (; ; )
            {
                yield return new Wait(RemoteEvents.AreaChanged);
                this.dpsTracker.Reset();
                this.defaultLargeMapCenter = null;
                this.isMenuOpen = false;
            }
        }

        private bool IsMenuOpen(InGameState inGame)
        {
            var largeMap = inGame.GameUi.LargeMap;
            var currentCenter = largeMap.Center;

            if (!this.defaultLargeMapCenter.HasValue)
            {
                this.defaultLargeMapCenter = currentCenter;
                this.isMenuOpen = false;
                return false;
            }

            const float menuThreshold = 0.00005f;
            var delta = Math.Abs(currentCenter.X - this.defaultLargeMapCenter.Value.X);

            if (delta >= menuThreshold)
            {
                this.isMenuOpen = true;
                return true;
            }

            this.isMenuOpen = false;
            this.defaultLargeMapCenter = currentCenter;
            return false;
        }
    }
}
