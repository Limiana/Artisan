﻿using Artisan.RawInformation;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.Toast;
using Dalamud.IoC;
using Dalamud.Plugin;
using System;
using static Artisan.CraftingLogic.CurrentCraft;

namespace Artisan
{
    public sealed class Artisan : IDalamudPlugin
    {
        public string Name => "Artisan";
        private const string commandName = "/artisan";
        private PluginUI PluginUi { get; init; }

        public Artisan(
            [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
            [RequiredVersion("1.0")] CommandManager commandManager)
        {
            FFXIVClientStructs.Resolver.Initialize();

            pluginInterface.Create<Service>();

            Service.Configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Service.Configuration.Initialize(Service.Interface);

            Service.Address = new PluginAddressResolver();
            Service.Address.Setup();

            ECommons.ECommons.Init(pluginInterface);
            this.PluginUi = new PluginUI(this);

            Service.CommandManager.AddHandler(commandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Opens the Artisan menu."
            });

            Service.Interface.UiBuilder.Draw += DrawUI;
            Service.Interface.UiBuilder.OpenConfigUi += DrawConfigUI;
            Service.Condition.ConditionChange += CheckForCraftedState;
            Service.Framework.Update += FireBot;
            ActionWatching.Enable();
            StepChanged += ResetRecommendation;

            UiHelper.Setup(Service.SigScanner);
        }

        public Artisan()
        {
        }

        private void ResetRecommendation(object? sender, int e)
        {
            CurrentRecommendation = 0;
        }

        private void FireBot(Framework framework)
        {
            if (!Service.Condition[ConditionFlag.Crafting]) PluginUi.CraftingVisible = false;
            GetCraft();

            if (CanUse(Skills.BasicSynth) && CurrentRecommendation == 0)
            {
                FetchRecommendation(CurrentStep, CurrentStep);
            }

#if DEBUG
            if (PluginUi.repeatTrial)
            {
                RepeatTrialCraft();
            }
#endif
            bool enableAutoRepeat = Service.Configuration.AutoCraft;
            if (enableAutoRepeat && Service.Condition[ConditionFlag.PreparingToCraft])
            {
                RepeatActualCraft();
            }

        }

        public static async void FetchRecommendation(object? sender, int e)
        {
            try
            {
                if (e == 0)
                {
                    CurrentRecommendation = 0;
                    ManipulationUsed = false;
                    JustUsedObserve = false;
                    VenerationUsed = false;
                    InnovationUsed = false;
                    WasteNotUsed = false;
                    JustUsedFinalAppraisal = false;
                    BasicTouchUsed = false;
                    StandardTouchUsed = false;
                    AdvancedTouchUsed = false;
                    ExpertCraftOpenerFinish = false;

                    return;
                }

                var rec = Recipe.IsExpert ? GetExpertRecommendation() : GetRecommendation();
                CurrentRecommendation = rec;

                if (rec != 0)
                {
                    if (LuminaSheets.ActionSheet.TryGetValue(rec, out var normalAct))
                    {
                        QuestToastOptions options = new QuestToastOptions() { IconId = normalAct.Icon };
                        Service.ToastGui.ShowQuest($"Use {normalAct.Name}", options);
                    }
                    if (LuminaSheets.CraftActions.TryGetValue(rec, out var craftAct))
                    {
                        QuestToastOptions options = new QuestToastOptions() { IconId = craftAct.Icon };
                        Service.ToastGui.ShowQuest($"Use {craftAct.Name}", options);
                    }

                    if (Service.Configuration.AutoMode)
                    {
                        Hotbars.ExecuteRecommended(rec);
                    }

                    return;
                }
            }
            catch (Exception ex)
            {
                Dalamud.Logging.PluginLog.Error(ex, "Crafting Step Change");
            }

        }

        private void CheckForCraftedState(ConditionFlag flag, bool value)
        {
            if (flag == ConditionFlag.Crafting && value)
            {
                this.PluginUi.CraftingVisible = true;
            }
        }

        public void Dispose()
        {
            this.PluginUi.Dispose();
            ECommons.ECommons.Dispose();

            Service.CommandManager.RemoveHandler(commandName);

            Service.Interface.UiBuilder.OpenConfigUi -= DrawConfigUI;
            Service.Interface.UiBuilder.Draw -= DrawUI;
            StepChanged -= FetchRecommendation;
            Service.Framework.Update -= FireBot;

            ActionWatching.Dispose();
        }

        private void OnCommand(string command, string args)
        {
            this.PluginUi.Visible = true;
        }

        private void DrawUI()
        {
            this.PluginUi.Draw();
        }

        private void DrawConfigUI()
        {
            this.PluginUi.Visible = true;
        }
    }
}
