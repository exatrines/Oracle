using Dalamud.Game.DutyState;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Network;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Oracle.Models;

namespace Oracle.Services;

/// <summary>
/// Dalamud plugin log of countdown, combat, Auto Load, enemy casts, status apply/remove, and action effects.
/// Owns its own Combat/Countdown detectors; does not share engine edges.
/// </summary>
internal sealed unsafe class PluginLogService : IDisposable
{
    private readonly ActionEffectReceiveHub _receiveHub;
    private readonly ActorCastReceiveHub _castHub;
    private readonly StatusManagerReceiveHub _statusHub;
    private readonly Clock _clock = new();
    private readonly CombatSyncDetector _combat = new();
    private readonly CountdownSyncDetector _countdown = new();

    public PluginLogService(
        ActionEffectReceiveHub receiveHub,
        ActorCastReceiveHub castHub,
        StatusManagerReceiveHub statusHub)
    {
        _receiveHub = receiveHub;
        _castHub = castHub;
        _statusHub = statusHub;
        _receiveHub.Received += OnActionEffectReceived;
        _castHub.Received += OnCastReceived;
        _statusHub.Received += OnStatusChanged;
        PluginServices.DutyState.DutyStarted += OnDutyStarted;
        PluginServices.DutyState.DutyRecommenced += OnDutyRecommenced;
        _countdown.Subscribe();
    }

    public void Dispose()
    {
        PluginServices.DutyState.DutyRecommenced -= OnDutyRecommenced;
        PluginServices.DutyState.DutyStarted -= OnDutyStarted;
        _statusHub.Received -= OnStatusChanged;
        _receiveHub.Received -= OnActionEffectReceived;
        _castHub.Received -= OnCastReceived;
        _countdown.Dispose();
    }

    public void Update()
    {
        _combat.Update();
        _countdown.Update();

        if (_countdown.JustStarted)
            _clock.StartCountdown(_countdown.StartedRemaining);

        if (_combat.JustEnteredCombat)
            _clock.StartCombat();

        if (C.PluginLogEnabled)
        {
            if (C.PluginLogCountdown && _countdown.JustStarted)
                Write("Countdown");
            if (C.PluginLogCombat && _combat.JustEnteredCombat)
                Write("Combat start");
            if (C.PluginLogCombat && _combat.JustLeftCombat)
                Write("Combat end");
        }

        if (_combat.JustLeftCombat)
            _clock.Stop();
    }

    private void OnActionEffectReceived(
        uint casterEntityId,
        Character* casterPtr,
        ActionEffectHandler.Header* header,
        ActionEffectHandler.TargetEffects* effects,
        GameObjectId* targetEntityIds)
    {
        if (!C.PluginLogEnabled || !C.PluginLogActionEffect)
            return;
        if (!EnemyHitRules.TryMatch(casterEntityId, header, targetEntityIds, out var hit))
            return;

        Write(
            $"Enemy hit {hit.ActionId} {hit.ActionName} caster={hit.CasterName} friendlyTargets={hit.FriendlyTargetCount}");
    }

    private void OnCastReceived(uint casterEntityId, ActorCastPacket* packet)
    {
        if (!C.PluginLogEnabled || !C.PluginLogCasts || packet == null)
            return;
        if (!EnemyHitRules.TryDescribeEnemyCast(
                casterEntityId,
                packet->ActionId,
                packet->SpellId,
                out var hit))
            return;

        var line =
            $"Enemy cast {hit.ActionId} {hit.ActionName} caster={hit.CasterName} time={packet->CastTime:0.00}";
        if (packet->SpellId != 0 && packet->SpellId != hit.ActionId)
            line += $" spell={packet->SpellId}";
        if (packet->ActionType != 1)
            line += $" type={packet->ActionType}";
        Write(line);
    }

    private void OnStatusChanged(uint statusId, bool removed, uint sourceEntityId)
    {
        if (!C.PluginLogEnabled || !C.PluginLogStatus || statusId == 0)
            return;

        var source = sourceEntityId == 0
            ? "0"
            : $"0x{sourceEntityId:X8}";
        Write(
            $"Status {(removed ? "remove" : "apply")} {statusId} {ActionLookup.GetStatusName(statusId)} source={source}");
    }

    private void OnDutyStarted(IDutyStateEventArgs args) =>
        WriteAutoLoad("Duty start");

    private void OnDutyRecommenced(IDutyStateEventArgs args) =>
        WriteAutoLoad("Duty restart");

    internal void WriteAutoLoad(string message)
    {
        if (C.PluginLogEnabled && C.PluginLogAutoLoad)
            Write(message);
    }

    private void Write(string message) =>
        PluginServices.Log.Information("[{Clock:l}] {Message:l}", _clock.FormatValue(), message);

    /// <summary>Countdown is negative; combat start is 0.</summary>
    private sealed class Clock
    {
        private readonly object _gate = new();
        private DateTime _syncUtc;
        private float _offset;
        private bool _running;

        public void StartCountdown(float remainingSeconds)
        {
            lock (_gate)
            {
                _syncUtc = DateTime.UtcNow;
                _offset = -Math.Abs(remainingSeconds);
                _running = true;
            }
        }

        public void StartCombat()
        {
            lock (_gate)
            {
                if (_running)
                    return;

                _syncUtc = DateTime.UtcNow;
                _offset = 0f;
                _running = true;
            }
        }

        public void Stop()
        {
            lock (_gate)
                _running = false;
        }

        public string FormatValue()
        {
            lock (_gate)
            {
                if (!_running)
                    return "--:--";

                var seconds = _offset + (float)(DateTime.UtcNow - _syncUtc).TotalSeconds;
                return CueTime.Format(seconds);
            }
        }
    }
}
