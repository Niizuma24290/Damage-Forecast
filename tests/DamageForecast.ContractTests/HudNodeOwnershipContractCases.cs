using DamageForecast.UI;

internal static class HudNodeOwnershipContractCases
{
    public static IEnumerable<ContractCase> Create()
    {
        yield return new(
            "HN-001",
            "HudNodeOwnership",
            "HudNode.NoCandidate_CreatesExactlyOneCanonicalNode",
            assert =>
            {
                var resolution = DamageForecastHudNodeOwnershipPolicy.Resolve([]);
                assert.True(
                    resolution.CreateNew
                    && !resolution.FailClosed
                    && resolution.CanonicalIndex == -1
                    && resolution.DuplicateOwnedIndexes.Count == 0,
                    "create=true; failClosed=false; canonical=-1; duplicates=0",
                    resolution.ToString());
            });

        yield return new(
            "HN-002",
            "HudNodeOwnership",
            "HudNode.ExactCorrectNode_IsReusedAcrossRepeatedRefreshes",
            assert =>
            {
                var candidate = new[] { new DamageForecastHudNodeCandidate(true, true, true) };
                var allReused = Enumerable.Range(0, 100)
                    .Select(_ => DamageForecastHudNodeOwnershipPolicy.Resolve(candidate))
                    .All(resolution => !resolution.CreateNew
                        && !resolution.FailClosed
                        && resolution.CanonicalIndex == 0
                        && resolution.DuplicateOwnedIndexes.Count == 0);
                assert.Equal(true, allReused, "100 repeated resolutions reuse the same canonical node");
            });

        yield return new(
            "HN-003",
            "HudNodeOwnership",
            "HudNode.ExactWrongType_FailsClosed",
            assert =>
            {
                var resolution = DamageForecastHudNodeOwnershipPolicy.Resolve(
                    [new DamageForecastHudNodeCandidate(true, false, false)]);
                assert.True(
                    resolution.FailClosed && !resolution.CreateNew && resolution.CanonicalIndex == -1,
                    "failClosed=true; create=false; canonical=-1",
                    resolution.ToString());
            });

        yield return new(
            "HN-004",
            "HudNodeOwnership",
            "HudNode.MultipleOwnedNodes_KeepCanonicalAndRetireDuplicates",
            assert =>
            {
                var resolution = DamageForecastHudNodeOwnershipPolicy.Resolve(
                [
                    new DamageForecastHudNodeCandidate(false, true, true),
                    new DamageForecastHudNodeCandidate(true, true, true),
                    new DamageForecastHudNodeCandidate(false, true, true),
                    new DamageForecastHudNodeCandidate(false, true, false)
                ]);
                assert.True(
                    !resolution.CreateNew
                    && !resolution.FailClosed
                    && resolution.CanonicalIndex == 1
                    && resolution.DuplicateOwnedIndexes.SequenceEqual([0, 2]),
                    "canonical=1; duplicate owned nodes=0,2; unowned node untouched",
                    $"canonical={resolution.CanonicalIndex}; duplicates={string.Join(',', resolution.DuplicateOwnedIndexes)}");
            });

        yield return new(
            "HN-005",
            "HudNodeOwnership",
            "HudNode.OwnedWrongTypeWithDifferentName_IsRetiredWithoutTakingOverUnownedNode",
            assert =>
            {
                var resolution = DamageForecastHudNodeOwnershipPolicy.Resolve(
                [
                    new DamageForecastHudNodeCandidate(true, true, false),
                    new DamageForecastHudNodeCandidate(false, false, true)
                ]);
                assert.True(
                    !resolution.CreateNew
                    && !resolution.FailClosed
                    && resolution.CanonicalIndex == 0
                    && resolution.DuplicateOwnedIndexes.SequenceEqual([1]),
                    "reuse the exact correct node; retire the separately named owned stale node",
                    $"canonical={resolution.CanonicalIndex}; duplicates={string.Join(',', resolution.DuplicateOwnedIndexes)}");
            });

        yield return new(
            "HN-006",
            "HudSurface",
            "HudSurface.MultiplayerPlayerSummary_IsExcluded",
            assert =>
            {
                var stateExcluded = DamageForecastHudSurfacePolicy.IsExcludedMultiplayerSummaryAncestor(
                    "MegaCrit.Sts2.Core.Nodes.Multiplayer.NMultiplayerPlayerState");
                var containerExcluded = DamageForecastHudSurfacePolicy.IsExcludedMultiplayerSummaryAncestor(
                    "MegaCrit.Sts2.Core.Nodes.Multiplayer.NMultiplayerPlayerStateContainer");
                assert.True(
                    stateExcluded && containerExcluded,
                    "state=true; container=true",
                    $"state={stateExcluded}; container={containerExcluded}");
            });

        yield return new(
            "HN-007",
            "HudSurface",
            "HudSurface.CharacterHealthBarHierarchy_RemainsSupported",
            assert =>
            {
                var healthBarExcluded = DamageForecastHudSurfacePolicy.IsExcludedMultiplayerSummaryAncestor(
                    "MegaCrit.Sts2.Core.Nodes.Combat.NHealthBar");
                var similarlyNamedExcluded = DamageForecastHudSurfacePolicy.IsExcludedMultiplayerSummaryAncestor(
                    "Example.NMultiplayerPlayerStatePreview");
                assert.True(
                    !healthBarExcluded && !similarlyNamedExcluded,
                    "healthBar=false; similarlyNamed=false",
                    $"healthBar={healthBarExcluded}; similarlyNamed={similarlyNamedExcluded}");
            });

        yield return new(
            "HN-008",
            "HudSurface",
            "HudSurface.SharedEndTurnRoot_IsHiddenOnlyByRegisteredLocalBar",
            assert =>
            {
                var unregisteredCanHide =
                    DamageForecastHudSurfacePolicy.CanHideSharedEndTurnSurface(false);
                var registeredCanHide =
                    DamageForecastHudSurfacePolicy.CanHideSharedEndTurnSurface(true);
                assert.True(
                    !unregisteredCanHide && registeredCanHide,
                    "unregistered=false; registered=true",
                    $"unregistered={unregisteredCanHide}; registered={registeredCanHide}");
            });

        yield return new(
            "HN-009",
            "HudSurface",
            "HudSurface.EndTurnAnchor_CurrentOwnerButtonAndMarker_Accepted",
            assert =>
            {
                var accepted = HudEndTurnAnchorOwnershipPolicy.CanUse(
                    ownerUsable: true,
                    buttonUsable: true,
                    ownerMatchesContext: true,
                    buttonBelongsToOwner: true,
                    markerPresent: true);
                assert.Equal(true, accepted);
            });

        yield return new(
            "HN-010",
            "HudSurface",
            "HudSurface.EndTurnAnchor_StaleDetachedOrUnmarkedButton_Rejected",
            assert =>
            {
                var staleOwner = HudEndTurnAnchorOwnershipPolicy.CanUse(
                    ownerUsable: true,
                    buttonUsable: true,
                    ownerMatchesContext: false,
                    buttonBelongsToOwner: true,
                    markerPresent: true);
                var detachedButton = HudEndTurnAnchorOwnershipPolicy.CanUse(
                    ownerUsable: true,
                    buttonUsable: true,
                    ownerMatchesContext: true,
                    buttonBelongsToOwner: false,
                    markerPresent: true);
                var unmarkedButton = HudEndTurnAnchorOwnershipPolicy.CanUse(
                    ownerUsable: true,
                    buttonUsable: true,
                    ownerMatchesContext: true,
                    buttonBelongsToOwner: true,
                    markerPresent: false);
                assert.True(
                    !staleOwner && !detachedButton && !unmarkedButton,
                    "previous combat owner, detached button, and missing marker are rejected",
                    $"stale={staleOwner}; detached={detachedButton}; unmarked={unmarkedButton}");
            });

        yield return new(
            "HN-011",
            "HudSurface",
            "HudSurface.EndTurnLiveAnchor_IsButtonLocalAndPositionIndependent",
            assert =>
            {
                var anchor = HudEndTurnLocalAnchorPolicy.Resolve(
                    new HudLayoutSize(312f, 84f));
                assert.True(
                    anchor == new HudLayoutRect(0f, 0f, 312f, 84f),
                    "x=0; y=0; width=312; height=84",
                    anchor.ToString());
            });

        yield return new(
            "HN-012",
            "HudSurface",
            "HudSurface.EndTurnLiveAnchor_UsesNativeLabelCenterButPreservesOuterVerticalBasis",
            assert =>
            {
                var anchor = HudEndTurnLocalAnchorPolicy.Resolve(
                    new HudLayoutSize(220f, 84f),
                    new HudLayoutRect(72f, 21f, 196f, 42f),
                    new HudLayoutRect(40f, 18f, 260f, 58f));
                assert.True(
                    anchor == new HudLayoutRect(72f, 0f, 196f, 84f),
                    "x and width use native label; y and height use outer button",
                    anchor.ToString());
            });

        yield return new(
            "HN-013",
            "HudSurface",
            "HudSurface.EndTurnLiveAnchor_ImageIsOnlyFallbackForMissingNativeLabel",
            assert =>
            {
                var anchor = HudEndTurnLocalAnchorPolicy.Resolve(
                    new HudLayoutSize(220f, 84f),
                    labelAnchor: null,
                    imageAnchor: new HudLayoutRect(40f, 18f, 260f, 58f));
                assert.Equal(new HudLayoutRect(40f, 0f, 260f, 84f), anchor);
            });
    }
}
