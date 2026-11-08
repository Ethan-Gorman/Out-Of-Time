using UnityEngine;
using UnityEngine.Rendering;

// Layout numbers for the Steampunk factory, shared by the builder and its prefabs.
//
//   x: -22 (left wall) .. +22 (right wall)      z: -10 (front, toward the camera) .. +9.5 (back wall)
//   Ground floor at y = 0. First floor (a mezzanine along the back) at y = FirstFloorY, z 2..9.5.
//
//   Left:   the gear tower (gears 1-3 stacked, the engine that drives gear 1 by chain).
//           Above it on the first floor: the time machine, and the broken boards the player falls through.
//   Ground: storage (-13..-5) -> workshop (-5..4) -> steam vents (x 6) -> conveyor + press (x 9.6) -> boilers -> lift (x 19).
//   First:  lift landing (x 19) -> vents (x 14) -> swinging crane hooks (x 8) -> narrow catwalk with a hook (x -3..4)
//           -> Clockwork Guard -> time machine (x -12).
public static class SteampunkFactory
{
    public const float FirstFloorY = 4.5f;
    public const float RoofY = 11f;
    public const float BeltLength = 19f;
    public const float VentDepth = 2.3f;
    public const float HookLength = 4.2f;
    public static readonly Vector3 ArrivalPoint = new Vector3(-9.8f, 7.4f, 2.8f);
}

public static partial class SteampunkSceneBuilder
{
    const float FF = SteampunkFactory.FirstFloorY;

    static void MakeLevel()
    {
        var root = new GameObject("SteampunkLevel");
        var building = Group("Building", root.transform);
        var tower = Group("Gear Tower", root.transform);
        var ground = Group("Ground Floor", root.transform);
        var first = Group("First Floor", root.transform);
        var people = Group("People", root.transform);

        MakeBuilding(building);
        var director = new GameObject("LevelDirector"); director.transform.SetParent(root.transform, false);
        var level = director.AddComponent<SteampunkLevel>();
        MakeTower(tower, level);
        MakeGroundFloor(ground);
        MakeFirstFloor(first, level);
        MakePeople(people, first);

        // The lift, at the right end, powered from the tower through the ceiling drive shaft
        var lift = Place("MechanicalLift", root.transform, new Vector3(19f, 0f, 0f));
        level.lift = lift.GetComponentInChildren<MechanicalLift>();
        // The drive runs high along the back wall (so it never crosses in front of the first floor), then over to the lift head
        const float shaftY = 10.2f, shaftZ = 8.9f;
        var shaft = Cyl("Drive shaft", root.transform, new Vector3(0.1f, shaftY, shaftZ), new Vector3(0.28f, 18.9f, 0.28f), ironMat, false, new Vector3(0, 0, 90));
        var shaftSpin = shaft.AddComponent<GearSpinner>(); shaftSpin.axis = Vector3.up; shaftSpin.degreesPerSecond = 160f; shaftSpin.running = false;
        var cross = Cyl("Lift cross shaft", root.transform, new Vector3(19f, shaftY + 0.4f, shaftZ * 0.5f), new Vector3(0.24f, shaftZ * 0.5f, 0.24f), ironMat, false, new Vector3(90, 0, 0));
        var crossSpin = cross.AddComponent<GearSpinner>(); crossSpin.axis = Vector3.up; crossSpin.degreesPerSecond = -160f; crossSpin.running = false;
        Cyl("Bevel box", root.transform, new Vector3(19f, shaftY + 0.2f, shaftZ), new Vector3(0.7f, 0.4f, 0.7f), copperMat);
        for (float x = -14f; x < 18f; x += 4.5f)
        {
            Box("Shaft hanger", root.transform, new Vector3(x, shaftY + 0.45f, shaftZ), new Vector3(0.15f, 0.9f, 0.15f), ironMat);
            Cyl("Shaft collar", root.transform, new Vector3(x, shaftY, shaftZ), new Vector3(0.45f, 0.12f, 0.45f), brassMat, false, new Vector3(0, 0, 90));
        }
        level.lift.machinery = new[] { shaftSpin, crossSpin };

        Save(root, Prefabs + "/SteampunkLevel.prefab");
    }

    // ---------------------------------------------------------------- shell: floor, walls, roof, windows

    static void MakeBuilding(Transform b)
    {
        // Floor: one collider, visual tiles (so each tile picks up its nearest lamps)
        Box("Floor collider", b, new Vector3(0, -0.25f, 0), new Vector3(44f, 0.5f, 20f), null, true);
        Tiles("Floor", b, new Vector3(-22, -0.5f, -10), new Vector3(22, 0, 10), floorMat, 4f);
        // A sooty yard outside, so the camera never looks past the walls into empty sky
        Box("Outside yard", b, new Vector3(0, -0.7f, 0), new Vector3(160f, 0.4f, 120f), soot);

        // Walls: brick, full height, except the camera side (front), which is a low brick knee wall;
        // above it the front wall only casts shadows, so the camera sees in
        Box("Back wall collider", b, new Vector3(0, RoofHalf(), 9.75f), new Vector3(44.6f, SteampunkFactory.RoofY, 0.5f), null, true);
        Tiles("Back wall", b, new Vector3(-22.3f, 0, 9.5f), new Vector3(22.3f, SteampunkFactory.RoofY, 10f), brickMat, 4f);
        foreach (float x in new[] { -22.25f, 22.25f })
        {
            Box("Side wall collider", b, new Vector3(x, RoofHalf(), 0), new Vector3(0.5f, SteampunkFactory.RoofY, 20f), null, true);
            Tiles("Side wall", b, new Vector3(x - 0.25f, 0, -10), new Vector3(x + 0.25f, SteampunkFactory.RoofY, 10), brickMat, 4f);
        }
        Box("Front knee wall", b, new Vector3(0, 0.45f, -9.75f), new Vector3(44.6f, 0.9f, 0.5f), brickMat, true);
        Box("Front knee cap", b, new Vector3(0, 0.93f, -9.75f), new Vector3(44.6f, 0.08f, 0.6f), brassMat);
        ShadowsOnly(Box("Front wall (shadow only)", b, new Vector3(0, 0.9f + (SteampunkFactory.RoofY - 0.9f) * 0.5f, -9.75f), new Vector3(44.6f, SteampunkFactory.RoofY - 0.9f, 0.5f), brickMat, true));

        // Roof: iron panels with glazed gaps between them. Shadows only, so it never hides the player,
        // but the sun still falls through the gaps in long cool shafts
        for (float x = -22f; x < 22f; x += 4.4f)
            ShadowsOnly(Box("Roof panel", b, new Vector3(x + 1.7f, SteampunkFactory.RoofY + 0.2f, 0), new Vector3(3.4f, 0.4f, 21f), ironMat));
        for (float x = -19.8f; x < 22f; x += 8.8f)
            ShadowsOnly(Box("Roof truss", b, new Vector3(x, SteampunkFactory.RoofY - 0.4f, 0), new Vector3(0.35f, 0.8f, 20f), ironMat));

        // Tall arched windows high on the back wall: cool daylight against the amber bulbs
        foreach (float x in new[] { -9.5f, -3.5f, 9.5f, 15.5f })
            ArchedWindow(b, new Vector3(x, FF + 3.6f, 9.47f));
        foreach (float x in new[] { -19f, -15f, 13f, 19f })
            ArchedWindow(b, new Vector3(x, 1.9f, 9.47f), 0.6f);
        Place("FactoryClock", b, new Vector3(3f, FF + 3.7f, 9.4f));
        Place("WallGear", b, new Vector3(-6.5f, FF + 4.6f, 9.35f));
        var wg = Place("WallGear", b, new Vector3(-5.1f, FF + 3.2f, 9.3f)); wg.transform.localScale = Vector3.one * 0.6f;
        wg.GetComponentInChildren<GearSpinner>().degreesPerSecond = -30f;
        Place("WallGear", b, new Vector3(20.5f, FF + 4.8f, 9.35f));

        // Flanged copper pipes along the back wall and down the side walls
        Pipe(b, new Vector3(-13f, 3.2f, 9.15f), new Vector3(22f, 3.2f, 9.15f), 0.22f, copperMat);
        Pipe(b, new Vector3(-13f, 2.6f, 9.2f), new Vector3(4f, 2.6f, 9.2f), 0.14f, brassMat);
        Pipe(b, new Vector3(-21.8f, FF + 5.6f, 9.1f), new Vector3(22f, FF + 5.6f, 9.1f), 0.3f, copperMat);
        Pipe(b, new Vector3(21.75f, 0.3f, 8f), new Vector3(21.75f, SteampunkFactory.RoofY, 8f), 0.3f, copperMat);
        Pipe(b, new Vector3(21.75f, 0.3f, -6f), new Vector3(21.75f, SteampunkFactory.RoofY, -6f), 0.22f, brassMat);
        Pipe(b, new Vector3(-21.75f, 0.3f, -5f), new Vector3(-21.75f, SteampunkFactory.RoofY, -5f), 0.3f, copperMat);
        for (float z = -8f; z < 9f; z += 4f)
            Lamp(b, new Vector3(-21.4f, 3.4f, z), 0f);
        for (float z = -8f; z < 1f; z += 4f)
            Lamp(b, new Vector3(21.4f, 3.4f, z), 0f);
    }

    static float RoofHalf() => SteampunkFactory.RoofY * 0.5f;

    static void ArchedWindow(Transform parent, Vector3 centre, float scale = 1f)
    {
        var w = Group("Arched window", parent); w.localPosition = centre; w.localScale = Vector3.one * scale;
        Box("Glass", w, Vector3.zero, new Vector3(2.4f, 3.6f, 0.04f), glassMat);
        Cyl("Arch glass", w, new Vector3(0, 1.8f, 0), new Vector3(2.4f, 0.02f, 2.4f), glassMat, false, new Vector3(90, 0, 0));
        Box("Frame L", w, new Vector3(-1.25f, 0, -0.03f), new Vector3(0.14f, 3.7f, 0.08f), brassMat);
        Box("Frame R", w, new Vector3(1.25f, 0, -0.03f), new Vector3(0.14f, 3.7f, 0.08f), brassMat);
        Box("Sill", w, new Vector3(0, -1.85f, -0.06f), new Vector3(2.8f, 0.16f, 0.18f), brassMat);
        Box("Mullion", w, new Vector3(0, 0.6f, -0.03f), new Vector3(0.08f, 4.6f, 0.06f), ironMat);
    }

    // ---------------------------------------------------------------- the gear tower

    static void MakeTower(Transform t, SteampunkLevel level)
    {
        const float gz = 1.6f; // gear plane
        float top = SteampunkFactory.RoofY - 0.4f;
        // Frame and back plate; the block behind the gears keeps the player in front of them
        Box("Tower body (collider)", t, new Vector3(-17.9f, top * 0.5f, 4.45f), new Vector3(7.8f, top, 5.9f), null, true);
        Box("Back plate", t, new Vector3(-17.9f, top * 0.5f, 2.3f), new Vector3(7.8f, top, 0.2f), ironMat);
        foreach (float x in new[] { -21.6f, -13.6f })
        {
            Box("Girder", t, new Vector3(x, top * 0.5f, 0.75f), new Vector3(0.35f, top, 0.35f), ironMat, true);
            Box("Girder", t, new Vector3(x, top * 0.5f, 7.3f), new Vector3(0.35f, top, 0.35f), ironMat);
        }
        foreach (float y in new[] { 3.7f, 6.6f, top })
            Box("Cross beam", t, new Vector3(-17.6f, y, 0.75f), new Vector3(8.2f, 0.3f, 0.3f), brassMat);
        Box("Brace", t, new Vector3(-20.3f, 5.2f, 0.85f), new Vector3(0.15f, 4f, 0.15f), ironMat).transform.localRotation = Quaternion.Euler(0, 0, 32);
        Box("Tower sign", t, new Vector3(-17.6f, top - 0.6f, 0.6f), new Vector3(3.2f, 0.6f, 0.06f), brassMat);

        // Gear 1: chain-driven from the engine. Gear 2: oiled. Gear 3: freed by the rod on gear 2
        var g1 = MeshObject("Gear 1", t, new Vector3(-18.6f, 2.1f, gz), GearMesh(20, 1.5f, 1.8f, 0.35f), brassMat);
        var g1Spin = g1.AddComponent<GearSpinner>(); g1Spin.degreesPerSecond = 40f; g1Spin.running = false;
        Cyl("Hub", g1.transform, new Vector3(0, 0, -0.2f), new Vector3(0.9f, 0.06f, 0.9f), ironMat, false, new Vector3(90, 0, 0));
        MeshObject("Sprocket", g1.transform, new Vector3(0, 0, -0.32f), GearMesh(10, 0.42f, 0.55f, 0.08f), ironMat);
        Spokes(g1.transform, 1.3f);

        var g2 = MeshObject("Gear 2", t, new Vector3(-16.0f, 5.0f, gz), GearMesh(16, 1.2f, 1.45f, 0.35f), copperMat);
        var g2Spin = g2.AddComponent<GearSpinner>(); g2Spin.degreesPerSecond = -55f; g2Spin.running = false;
        Cyl("Hub", g2.transform, new Vector3(0, 0, -0.2f), new Vector3(0.7f, 0.06f, 0.7f), ironMat, false, new Vector3(90, 0, 0));
        Spokes(g2.transform, 1.05f);
        var pin2 = Cyl("Crank pin", g2.transform, new Vector3(0.75f, 0, -0.35f), new Vector3(0.2f, 0.12f, 0.2f), brassMat, false, new Vector3(90, 0, 0));

        var g3 = MeshObject("Gear 3", t, new Vector3(-18.8f, 7.9f, gz), GearMesh(14, 0.95f, 1.2f, 0.35f), brassMat);
        var g3Spin = g3.AddComponent<GearSpinner>(); g3Spin.running = false;
        Cyl("Hub", g3.transform, new Vector3(0, 0, -0.2f), new Vector3(0.6f, 0.06f, 0.6f), ironMat, false, new Vector3(90, 0, 0));
        Spokes(g3.transform, 0.85f);
        var pin3 = Cyl("Crank pin", g3.transform, new Vector3(0.75f, 0, -0.35f), new Vector3(0.2f, 0.12f, 0.2f), brassMat, false, new Vector3(90, 0, 0));
        // Gear 3's axle climbs to the ceiling drive shaft that runs the lift
        Cyl("Riser shaft", t, new Vector3(-18.8f, 9.1f, gz + 0.3f), new Vector3(0.22f, 1.1f, 0.22f), ironMat);
        Cyl("Bevel box", t, new Vector3(-18.8f, 10.2f, gz + 0.3f), new Vector3(0.7f, 0.4f, 0.7f), copperMat);

        var rod = Box("Connecting rod", t, new Vector3(-17.4f, 6.4f, gz - 0.42f), new Vector3(0.16f, 4f, 0.08f), ironMat);
        var brakePivot = Group("Brake", t); brakePivot.localPosition = new Vector3(-20.7f, 8.9f, gz - 0.3f);
        Box("Brake arm", brakePivot, new Vector3(0.75f, 0, 0), new Vector3(1.6f, 0.18f, 0.12f), redMat);
        Box("Brake shoe", brakePivot, new Vector3(1.45f, -0.25f, 0), new Vector3(0.3f, 0.4f, 0.16f), soot);
        Sphere("Brake pivot", brakePivot, Vector3.zero, Vector3.one * 0.25f, brassMat);

        var link = new GameObject("Gear 3 link").AddComponent<GearLink>(); link.transform.SetParent(t, false);
        link.gear2 = g2Spin; link.gear3 = g3Spin; link.rod = rod.transform; link.pin2 = pin2.transform; link.pin3 = pin3.transform; link.brake = brakePivot;
        link.releasePuff = Emitter("Release steam", t, new Vector3(-18.8f, 7.9f, 0.9f), 0f, 3f, 0.4f, 1.3f); link.releasePuff.spread = 0.8f; link.releasePuff.burstCount = 18;
        level.gear3 = link;

        // The engine that drives gear 1: always running (steam, flywheel), but its chain has snapped
        Box("Engine", t, new Vector3(-14.6f, 0.75f, 2.7f), new Vector3(1.8f, 1.5f, 1.6f), ironMat, true);
        Cyl("Engine cylinder", t, new Vector3(-14.6f, 1.75f, 2.7f), new Vector3(0.8f, 0.5f, 0.8f), copperMat);
        Cyl("Engine stack", t, new Vector3(-14.2f, 3.0f, 3.1f), new Vector3(0.3f, 0.9f, 0.3f), ironMat);
        var engineSteam = Emitter("Engine steam", t, new Vector3(-14.2f, 3.95f, 3.1f), 4f, 1.2f, 0.3f, 1.1f); engineSteam.lifetime = 1.8f;
        var fly = MeshObject("Flywheel", t, new Vector3(-14.6f, 1.1f, gz), GearMesh(12, 0.75f, 0.9f, 0.25f), ironMat);
        var flySpin = fly.AddComponent<GearSpinner>(); flySpin.degreesPerSecond = 90f;
        MeshObject("Sprocket", fly.transform, new Vector3(0, 0, -0.25f), GearMesh(10, 0.42f, 0.55f, 0.08f), brassMat);
        Spokes(fly.transform, 0.65f);

        // Gear 1 repair point: the snapped chain lies on the floor; once fitted, a chain loop runs engine -> gear 1
        var r1 = new GameObject("Gear 1 (fit chain)"); r1.transform.SetParent(t, false); r1.transform.localPosition = new Vector3(-18.0f, 0f, -0.2f);
        var r1Range = r1.AddComponent<SphereCollider>(); r1Range.isTrigger = true; r1Range.center = new Vector3(0, 0.7f, 0); r1Range.radius = 1.4f;
        var broken = Group("Snapped chain", r1.transform);
        for (int i = 0; i < 7; i++) Box("Link", broken, new Vector3(-0.6f + i * 0.17f, 0.05f, 0.4f + Mathf.Sin(i) * 0.1f), new Vector3(0.2f, 0.08f, 0.12f), ironMat).transform.localRotation = Quaternion.Euler(i % 2 * 90, i * 17, 0);
        for (int i = 0; i < 5; i++) Box("Dangling link", broken, new Vector3(-0.6f, 1.9f - i * 0.2f, 1.45f), new Vector3(0.09f, 0.2f, 0.12f), ironMat).transform.localRotation = Quaternion.Euler(0, i % 2 * 90, 0);
        var loopGo = new GameObject("Drive chain"); loopGo.transform.SetParent(r1.transform, false);
        loopGo.transform.position = new Vector3(0, 0, gz - 0.38f);
        var loop = loopGo.AddComponent<ChainLoop>(); loop.drive = g1Spin; loop.centerA = new Vector2(-18.6f, 2.1f); loop.centerB = new Vector2(-14.6f, 1.1f); loop.radius = 0.5f; loop.linkCount = 44; loop.material = ironMat;
        var gear1 = r1.AddComponent<GearRepair>(); gear1.needs = SteampunkLevel.Item.Chain; gear1.spinner = g1Spin; gear1.brokenVisual = broken.gameObject; gear1.repairedVisual = loopGo;
        gear1.repairPrompt = "Fit the spare chain to Gear 1"; gear1.inspectPrompt = "Inspect Gear 1";
        gear1.missingItemLines = new[] { "PLAYER: \"Gear one's drive chain has snapped. There must be a spare in storage.\"" };
        gear1.repairPuff = Emitter("Repair steam", r1.transform, new Vector3(0, 0.6f, 1.2f), 0f, 2.2f, 0.35f, 1f);
        level.gear1 = gear1;

        // Gear 2 repair point: an oil cup and feed pipe running up to the seized gear
        var r2 = new GameObject("Gear 2 (oil)"); r2.transform.SetParent(t, false); r2.transform.localPosition = new Vector3(-15.4f, 0f, -0.3f);
        var r2Range = r2.AddComponent<SphereCollider>(); r2Range.isTrigger = true; r2Range.center = new Vector3(0, 0.7f, 0); r2Range.radius = 1.2f;
        Cyl("Oil feed pipe", r2.transform, new Vector3(-0.3f, 2.2f, 1.5f), new Vector3(0.1f, 2.0f, 0.1f), brassMat);
        Cyl("Oil cup", r2.transform, new Vector3(-0.3f, 0.9f, 1.4f), new Vector3(0.45f, 0.18f, 0.45f), brassMat);
        Cyl("Cup stand", r2.transform, new Vector3(-0.3f, 0.4f, 1.4f), new Vector3(0.12f, 0.4f, 0.12f), ironMat);
        var seized = Group("Seized tag", r2.transform);
        Box("Red tag", seized, new Vector3(-0.3f, 1.35f, 1.15f), new Vector3(0.45f, 0.3f, 0.03f), redMat);
        var oiled = Group("Oiled", r2.transform);
        Cyl("Oil sheen", oiled, new Vector3(-0.3f, 1.08f, 1.4f), new Vector3(0.36f, 0.01f, 0.36f), soot);
        Light("Oil glint", oiled, new Vector3(-0.3f, 1.5f, 1.0f), Hex("FFD27A"), 0.8f, 2f);
        var gear2 = r2.AddComponent<GearRepair>(); gear2.needs = SteampunkLevel.Item.Oil; gear2.spinner = g2Spin; gear2.drivenBy = gear1;
        gear2.brokenVisual = seized.gameObject; gear2.repairedVisual = oiled.gameObject;
        gear2.repairPrompt = "Oil the seized Gear 2"; gear2.inspectPrompt = "Inspect Gear 2";
        gear2.missingItemLines = new[] { "PLAYER: \"Gear two is rusted solid. It needs oil.\"" };
        gear2.repairPuff = Emitter("Repair steam", r2.transform, new Vector3(-0.3f, 1.3f, 1.2f), 0f, 1.8f, 0.3f, 0.9f);
        level.gear2 = gear2;

        // Lamps at the tower's sides, so they light the gears without hanging in front of them
        Lamp(t, new Vector3(-21.2f, 3.6f, 0.3f), 0f);
        Lamp(t, new Vector3(-13.9f, 6.6f, 0.3f), 0f);
        Light("Gear light", t, new Vector3(-17.4f, 5f, -1.2f), Hex("FFB55A"), 1.6f, 7f);
    }

    static void Spokes(Transform gear, float length)
    {
        for (int i = 0; i < 3; i++)
            Box("Spoke", gear, new Vector3(0, 0, -0.19f), new Vector3(length * 1.8f, 0.16f, 0.04f), ironMat).transform.localRotation = Quaternion.Euler(0, 0, i * 60f);
    }

    // ---------------------------------------------------------------- ground floor

    static void MakeGroundFloor(Transform g)
    {
        var storage = Group("1 Storage", g); var workshop = Group("2 Workshop", g); var production = Group("3 Production", g);

        // Storage: shelving under the first floor, crates and barrels, the spare chain between the shelves
        foreach (float x in new[] { -11.6f, -8.8f, -6.0f }) Place("StorageShelf", storage, new Vector3(x, 0, 8.6f));
        foreach (var p in new[] { new Vector3(-12.3f, 0, 5.4f), new Vector3(-11.1f, 0, 5.1f), new Vector3(-12.4f, 0, -7.9f), new Vector3(-11.3f, 0, -8.5f), new Vector3(-7.2f, 0, -8.6f), new Vector3(-12.5f, 0, -1.6f) })
            Place("Crate", storage, p, p.x * 37f % 20f);
        Place("Crate", storage, new Vector3(-12.3f, 1f, 5.4f), 12f); Place("Crate", storage, new Vector3(-12.4f, 1f, -7.9f), -8f);
        foreach (var p in new[] { new Vector3(-9.5f, 0, -8.6f), new Vector3(-8.6f, 0, -8.1f), new Vector3(-6.4f, 0, 5.2f), new Vector3(-12.6f, 0, 1.0f) })
            Place("Barrel", storage, p);
        Place("SpareChain", storage, new Vector3(-8.3f, 0, 6.3f));
        // The broken boards the player fell through, scattered under the hole
        for (int i = 0; i < 6; i++)
            Box("Broken plank", storage, new Vector3(-10.6f + i * 0.45f, 0.06f + (i % 2) * 0.05f, 2.2f + Mathf.Sin(i * 2f) * 0.7f), new Vector3(1.3f, 0.08f, 0.25f), darkWoodMat).transform.localRotation = Quaternion.Euler(0, i * 47f, (i % 3) * 6f);
        Box("Bent plate", storage, new Vector3(-9.2f, 0.12f, 1.4f), new Vector3(1.2f, 0.06f, 0.8f), ironMat).transform.localRotation = Quaternion.Euler(8, 25, 12);

        // Workshop: the mechanic's bench and oil can, a console against the back wall
        Place("Workbench", workshop, new Vector3(-0.6f, 0, 8.5f));
        Place("ControlConsole", workshop, new Vector3(2.9f, 0, 8.6f));
        Place("OilCan", workshop, new Vector3(1.6f, 0, 6.4f));
        Place("Barrel", workshop, new Vector3(-3.4f, 0, 8.7f));
        Place("Crate", workshop, new Vector3(-3.5f, 0, -8.5f), 15f);

        // Production: a wall of steam vents (staggered, so the safe gap travels along it), boilers and crates to close the
        // line off under the first floor, then the conveyor with its stamping press
        for (int i = 0; i < 6; i++)
        {
            var vent = Place("SteamVent", production, new Vector3(6.2f, 0, -8.35f + i * SteampunkFactory.VentDepth));
            vent.GetComponent<SteamVent>().phaseOffset = i * 0.16f;
        }
        Place("Crate", production, new Vector3(6.2f, 0, 4.8f)); Place("Boiler", production, new Vector3(6.2f, 0, 6.4f));
        Place("Crate", production, new Vector3(6.2f, 0, 8.0f)); Place("Crate", production, new Vector3(6.2f, 0, 9.0f), 10f);
        Place("Crate", production, new Vector3(6.2f, 1f, 8.5f), 30f);

        Place("ConveyorBelt", production, new Vector3(9.6f, 0, 0));
        Place("SteamPress", production, new Vector3(9.6f, 0, -3.0f));
        Place("SteamPress", production, new Vector3(9.6f, 0, 4.0f)).GetComponent<SteamPress>().phaseOffset = 0.5f;

        // Furnace corner: boilers, a glowing furnace throwing sparks, and the pocket watch tucked between them
        Place("Boiler", production, new Vector3(15.6f, 0, 7.0f));
        Place("Boiler", production, new Vector3(12.6f, 0, 8.3f));
        Box("Furnace", production, new Vector3(14.6f, 1.0f, 3.6f), new Vector3(2.2f, 2.0f, 1.6f), soot, true);
        Box("Furnace mouth", production, new Vector3(14.6f, 0.8f, 2.78f), new Vector3(1.2f, 0.8f, 0.04f), furnaceMat);
        Light("Furnace light", production, new Vector3(14.6f, 1.0f, 2.0f), Hex("FF7A2A"), 2.6f, 6f).gameObject.AddComponent<TorchFlicker>().flickerAmount = 0.2f;
        var sparks = Emitter("Furnace sparks", production, new Vector3(14.6f, 1.2f, 2.6f), 5f, 2f, 0.12f, 0.06f);
        sparks.sprite = sparkSprite; sparks.direction = new Vector3(0, 1, -0.6f); sparks.spread = 0.5f; sparks.gravity = 3f; sparks.startColor = Hex("FFD27A"); sparks.endColor = new Color(1f, 0.35f, 0.05f, 0f);
        Place("PocketWatch", production, new Vector3(13.9f, 0, 6.2f));
        Place("ControlConsole", production, new Vector3(16.5f, 0, -8.5f));
        Place("Barrel", production, new Vector3(13.2f, 0, -8.6f)); Place("Crate", production, new Vector3(21.2f, 0, 8.6f));

        // Under-floor lamps, and lamps hanging on long chains in the tall space in front
        foreach (float x in new[] { -10f, -3f, 3f, 12.5f, 18f }) Lamp(g, new Vector3(x, 3.6f, 4.2f), 0.4f);
        foreach (float x in new[] { -9f, 0f, 9.6f, 17f }) Lamp(g, new Vector3(x, 6.3f, -5.5f), SteampunkFactory.RoofY - 6.3f);
    }

    // ---------------------------------------------------------------- first floor (the mezzanine)

    static void MakeFirstFloor(Transform f, SteampunkLevel level)
    {
        // Floor pieces (x from, x to, z from, z to). The gap at x -11..-8.6 / z 2..3.6 is where the player fell through
        var pieces = new[]
        {
            new Vector4(12f, 22f, 2f, 9.5f),   // A: lift landing
            new Vector4(4f, 12f, 2f, 9.5f),    // B: crane hooks
            new Vector4(-3f, 4f, 5.2f, 7.0f),  // C: narrow catwalk
            new Vector4(-13f, -3f, 3.6f, 9.5f),// D: guard + time machine
            new Vector4(-8.6f, -3f, 2f, 3.6f),
            new Vector4(-13f, -11f, 2f, 3.6f),
        };
        foreach (var p in pieces)
        {
            Vector3 min = new Vector3(p.x, FF - 0.4f, p.z), max = new Vector3(p.y, FF, p.w);
            // Each tile is its own collider: the OccluderFader only cuts renderers on (or under) the collider
            // it hits, so a separate invisible collider would hide the player underneath without ever being cut
            Tiles("First floor", f, min, max, floorMat, 4f, true);
        }
        // Girders under the front edge, iron columns down to the ground floor
        foreach (var span in new[] { new Vector2(4f, 22f), new Vector2(-8.6f, -3f), new Vector2(-13f, -11f) })
            Box("Edge girder", f, new Vector3((span.x + span.y) * 0.5f, FF - 0.6f, 2.1f), new Vector3(span.y - span.x, 0.4f, 0.3f), brassMat);
        Box("Catwalk girder", f, new Vector3(0.5f, FF - 0.6f, 6.1f), new Vector3(7f, 0.4f, 0.3f), ironMat);
        foreach (var c in new[] { new Vector3(-12.4f, 0, 2.3f), new Vector3(-5.2f, 0, 2.3f), new Vector3(4.4f, 0, 2.3f), new Vector3(11.6f, 0, 2.3f), new Vector3(16.8f, 0, 2.3f), new Vector3(0.5f, 0, 6.1f) })
        {
            Cyl("Column", f, c + new Vector3(0, FF * 0.5f - 0.2f, 0), new Vector3(0.4f, FF * 0.5f - 0.2f, 0.4f), ironMat, true);
            Cyl("Capital", f, c + new Vector3(0, FF - 0.5f, 0), new Vector3(0.6f, 0.1f, 0.6f), brassMat);
            Cyl("Base", f, c + new Vector3(0, 0.1f, 0), new Vector3(0.6f, 0.1f, 0.6f), brassMat);
        }
        // Splintered boards round the hole
        for (int i = 0; i < 4; i++)
            Box("Splintered board", f, new Vector3(-10.8f + i * 0.6f, FF - 0.1f, 3.5f), new Vector3(0.25f, 0.08f, 0.6f + (i % 2) * 0.4f), darkWoodMat).transform.localRotation = Quaternion.Euler(25 + i * 8, 0, 0);

        // Railings: the front edge (gap for the lift), round the hole, the ends of the catwalk, the tower end
        Railing(f, new Vector3(4f, FF, 2f), new Vector3(17.4f, FF, 2f));
        Railing(f, new Vector3(20.6f, FF, 2f), new Vector3(22f, FF, 2f));
        Railing(f, new Vector3(-8.6f, FF, 2f), new Vector3(-3f, FF, 2f));
        Railing(f, new Vector3(-13f, FF, 2f), new Vector3(-11f, FF, 2f));
        Railing(f, new Vector3(-11f, FF, 3.6f), new Vector3(-8.6f, FF, 3.6f));
        Railing(f, new Vector3(-11f, FF, 2f), new Vector3(-11f, FF, 3.6f));
        Railing(f, new Vector3(-8.6f, FF, 2f), new Vector3(-8.6f, FF, 3.6f));
        Railing(f, new Vector3(-13f, FF, 2f), new Vector3(-13f, FF, 9.5f));
        foreach (float x in new[] { 4f, -3f })
        {
            Railing(f, new Vector3(x, FF, 2f), new Vector3(x, FF, 5.2f));
            Railing(f, new Vector3(x, FF, 7f), new Vector3(x, FF, 9.5f));
        }

        // A: steam vents across the walkway (and a pipe stack closing the gap by the wall)
        for (int i = 0; i < 3; i++)
            Place("SteamVent", f, new Vector3(14.3f, FF, 3.1f + i * SteampunkFactory.VentDepth)).GetComponent<SteamVent>().phaseOffset = 0.15f + i * 0.3f;
        Box("Pipe stack", f, new Vector3(14.3f, FF + 0.75f, 9.2f), new Vector3(1.6f, 1.5f, 0.7f), copperMat, true);
        Place("ControlConsole", f, new Vector3(17.6f, FF, 8.9f));
        Place("Boiler", f, new Vector3(20.8f, FF, 8.3f)).transform.localScale = Vector3.one * 0.8f;
        Place("Crate", f, new Vector3(12.6f, FF, 9.0f), 20f);

        // B and the catwalk: crane hooks swinging from an overhead rail
        float pivotY = FF + 1.2f + SteampunkFactory.HookLength;
        Box("Crane rail", f, new Vector3(4f, pivotY + 0.35f, 6f), new Vector3(16f, 0.25f, 0.25f), ironMat);
        Box("Crane rail", f, new Vector3(8f, pivotY + 0.35f, 6f), new Vector3(0.25f, 0.25f, 7.5f), ironMat);
        var hooks = new[] { new Vector3(8f, pivotY, 3.3f), new Vector3(8f, pivotY, 6.0f), new Vector3(8f, pivotY, 8.6f), new Vector3(0.5f, pivotY, 6.1f) };
        for (int i = 0; i < hooks.Length; i++)
        {
            var axe = Place("CraneHook", f, hooks[i]).GetComponent<SwingingAxe>();
            axe.phaseOffset = i * 0.33f;
        }
        Place("Barrel", f, new Vector3(5.0f, FF, 3.0f)); Place("Crate", f, new Vector3(11.3f, FF, 9.0f));

        // D: the Clockwork Guard's beat, and the time machine by the tower
        var guard = Place("ClockworkGuard", f, new Vector3(-7f, FF, 6.8f)).GetComponent<ClockworkGuard>();
        guard.patrolA = new Vector3(-10f, FF, 6.8f); guard.patrolB = new Vector3(-4.2f, FF, 6.8f);
        var machine = Place("SteampunkTimeMachine", f, new Vector3(-11.6f, FF, 7.6f));
        level.timeMachine = machine.GetComponent<TimeMachine>();
        Place("Crate", f, new Vector3(-12.4f, FF, 4.4f)); Place("Barrel", f, new Vector3(-4f, FF, 9.0f));

        foreach (float x in new[] { 19f, 11f, 1f, -7.5f }) Lamp(f, new Vector3(x, FF + 3.4f, 5.6f), SteampunkFactory.RoofY - FF - 3.4f);
    }

    static void MakePeople(Transform p, Transform first)
    {
        Place("WarehouseWorker", p, new Vector3(-10.4f, 0, -1.8f));
        Place("Mechanic", p, new Vector3(-1.8f, 0, 6.6f));
        Place("FactoryWorker", p, new Vector3(14.5f, 0, -6.0f));
        Place("SeniorEngineer", p, new Vector3(21f, FF, 4.6f));
        Place("MachineOperator", p, new Vector3(17.6f, FF, 7.6f));
        var supervisor = Place("Supervisor", p, new Vector3(5.6f, FF, 8.4f)).GetComponent<CaveNpc>();
        supervisor.orbitCenter = new Vector3(5.6f, FF, 7.9f); supervisor.orbitRadius = 0.6f; supervisor.orbitSpeed = 0.5f;
    }

    // ---------------------------------------------------------------- pieces

    // Brass railing along a line at floor height y: posts, top and mid rails, and a tall invisible collider so the
    // player can't jump over it
    static void Railing(Transform parent, Vector3 a, Vector3 b)
    {
        var r = Group("Railing", parent);
        Vector3 d = b - a; float len = d.magnitude; Vector3 mid = (a + b) * 0.5f;
        bool alongX = Mathf.Abs(d.x) > Mathf.Abs(d.z);
        Vector3 size(float t, float h) => alongX ? new Vector3(len, h, t) : new Vector3(t, h, len);
        Box("Railing collider", r, mid + Vector3.up * 1.1f, size(0.15f, 2.2f), null, true);
        Box("Top rail", r, mid + Vector3.up * 1.0f, size(0.09f, 0.09f), brassMat);
        Box("Mid rail", r, mid + Vector3.up * 0.55f, size(0.05f, 0.05f), ironMat);
        int posts = Mathf.Max(1, Mathf.RoundToInt(len / 1.3f));
        for (int i = 0; i <= posts; i++)
            Box("Post", r, Vector3.Lerp(a, b, i / (float)posts) + Vector3.up * 0.5f, new Vector3(0.08f, 1.0f, 0.08f), brassMat);
    }

    static void Pipe(Transform parent, Vector3 a, Vector3 b, float radius, Material mat)
    {
        Vector3 d = b - a; float len = d.magnitude;
        var pipe = Cyl("Pipe", parent, (a + b) * 0.5f, new Vector3(radius * 2f, len * 0.5f, radius * 2f), mat);
        pipe.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
        int flanges = Mathf.Max(2, Mathf.RoundToInt(len / 3f));
        for (int i = 0; i <= flanges; i++)
        {
            var fl = Cyl("Flange", parent, Vector3.Lerp(a, b, i / (float)flanges), new Vector3(radius * 2.7f, 0.06f, radius * 2.7f), brassMat);
            fl.transform.localRotation = pipe.transform.localRotation;
        }
    }

    // Hanging lamp; chain = length of chain above it (0 for a wall lamp on a bracket)
    static void Lamp(Transform parent, Vector3 pos, float chain)
    {
        Place("FactoryLamp", parent, pos);
        if (chain > 0.05f) Box("Lamp chain", parent, pos + Vector3.up * (1.6f + chain * 0.5f), new Vector3(0.05f, chain, 0.05f), soot);
    }

    // Splits a box into tiles: lights are per object, so one huge floor would only ever see a handful of lamps
    static void Tiles(string name, Transform parent, Vector3 min, Vector3 max, Material mat, float tile, bool solid = false)
    {
        var group = Group(name, parent);
        Vector3 size = max - min;
        int nx = Mathf.Max(1, Mathf.CeilToInt(size.x / tile - 0.01f)), ny = Mathf.Max(1, Mathf.CeilToInt(size.y / tile - 0.01f)), nz = Mathf.Max(1, Mathf.CeilToInt(size.z / tile - 0.01f));
        for (int i = 0; i < nx; i++) for (int j = 0; j < ny; j++) for (int k = 0; k < nz; k++)
        {
            Vector3 a = new Vector3(min.x + size.x * i / nx, min.y + size.y * j / ny, min.z + size.z * k / nz);
            Vector3 c = new Vector3(min.x + size.x * (i + 1) / nx, min.y + size.y * (j + 1) / ny, min.z + size.z * (k + 1) / nz);
            Box(name, group, (a + c) * 0.5f, c - a, mat, solid);
        }
    }

    static GameObject ShadowsOnly(GameObject go)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        return go;
    }
}
