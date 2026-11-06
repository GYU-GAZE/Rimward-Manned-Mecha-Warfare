namespace ZenithFrame
{
    // Pure eligibility policy; ownership changes remain in the game-facing component.
    public static class PilotRules
    {
        public static bool CanBoard(bool occupied, bool frameReady, bool pilotReady,
            bool sameFaction, bool sameMap, bool alreadyOwned, bool adjacent)
            => !occupied && frameReady && pilotReady && sameFaction && sameMap && !alreadyOwned && adjacent;

        public static bool CanOperate(bool hasPilot, bool frameReady, bool pilotReady, bool sameFaction)
            => hasPilot && frameReady && pilotReady && sameFaction;

        public static bool CanExecuteActions(bool canOperate, bool serviceLocked, bool stunned)
            => canOperate && !serviceLocked && !stunned;
    }
}
