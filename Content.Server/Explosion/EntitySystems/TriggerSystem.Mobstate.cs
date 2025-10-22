﻿using Content.Server.Explosion.Components;
using Content.Shared.Explosion.Components;
using Content.Shared.FloofStation;
using Content.Shared.Implants;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Verbs;

namespace Content.Server.Explosion.EntitySystems;

public sealed partial class TriggerSystem
{
    private void InitializeMobstate()
    {
        SubscribeLocalEvent<TriggerOnMobstateChangeComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<TriggerOnMobstateChangeComponent, SuicideEvent>(OnSuicide);

        SubscribeLocalEvent<TriggerOnMobstateChangeComponent, ImplantRelayEvent<SuicideEvent>>(OnSuicideRelay);
        SubscribeLocalEvent<TriggerOnMobstateChangeComponent, ImplantRelayEvent<MobStateChangedEvent>>(OnMobStateRelay);
        SubscribeLocalEvent<TriggerOnMobstateChangeComponent, ImplantRelayEvent<GetVerbsEvent<Verb>>>(OnVerbRelay);
    }

    private void OnMobStateChanged(
        EntityUid uid,
        TriggerOnMobstateChangeComponent component,
        MobStateChangedEvent args)
    {
        if (!component.MobState.Contains(args.NewMobState))
            return;

        if (!component.Enabled)
            return;

        if (component.PreventVore)
        {
            if (HasComp<VoredComponent>(args.Target))
            {
                // Typically, if someone is vored, they dont want people to come rush to
                // their aid, so just block the trigger if they are vored.
                return;
            }
        }

        //This chains Mobstate Changed triggers with OnUseTimerTrigger if they have it
        //Very useful for things that require a mobstate change and a timer
        if (TryComp<OnUseTimerTriggerComponent>(uid, out var timerTrigger))
        {
            HandleTimerTrigger(
                uid,
                args.Origin,
                timerTrigger.Delay,
                timerTrigger.BeepInterval,
                timerTrigger.InitialBeepDelay,
                timerTrigger.BeepSound);
        }
        else
            Trigger(uid);
    }

    /// <summary>
    /// Checks if the user has any implants that prevent suicide to avoid some cheesy strategies
    /// Prevents suicide by handling the event without killing the user
    /// </summary>
    private void OnSuicide(EntityUid uid, TriggerOnMobstateChangeComponent component, SuicideEvent args)
    {
        if (args.Handled)
            return;

        if (!component.PreventSuicide)
            return;

        _popupSystem.PopupEntity(
            Loc.GetString("suicide-prevented"),
            args.Victim,
            args.Victim);
        args.Handled = true;
    }

    private void OnSuicideRelay(EntityUid uid,
        TriggerOnMobstateChangeComponent component,
        ImplantRelayEvent<SuicideEvent> args)
    {
        OnSuicide(
            uid,
            component,
            args.Event);
    }

    private void OnMobStateRelay(EntityUid uid,
        TriggerOnMobstateChangeComponent component,
        ImplantRelayEvent<MobStateChangedEvent> args)
    {
        OnMobStateChanged(
            uid,
            component,
            args.Event);
    }

    private void OnVerbRelay(EntityUid uid,
        TriggerOnMobstateChangeComponent component,
        ImplantRelayEvent<GetVerbsEvent<Verb>> args)
    {
        OnGetVerbs(uid, component, args.Event);
    }

    private void OnGetVerbs(EntityUid uid,
        TriggerOnMobstateChangeComponent component,
        GetVerbsEvent<Verb> args)
    {
        if (args.User != args.Target)
            return; // Self only, but usable in crit

        var verb = new Verb()
        {
            Text = Loc.GetString(
                "trigger-on-mobstate-verb-text",
                ("state", component.Enabled ? "ON" : "OFF")),
            Act = () =>
            {
                component.Enabled = !component.Enabled;
                _popupSystem.PopupEntity(
                    Loc.GetString(
                        "trigger-on-mobstate-verb-popup",
                        ("state", component.Enabled ? "ENABLED" : "DISABLED")),
                    args.User,
                    args.User);
            },
            Disabled = false,
            Message = "Toggle whether or not this thing tells everyone you are dead both inside and outside."
        };
        args.Verbs.Add(verb);
    }
}
