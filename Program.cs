using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

try
{
    if (GtkNative.gtk_init_check() == 0)
    {
        Console.Error.WriteLine("Factory Frontier nu găsește o sesiune grafică Linux activă. Pornește jocul din mediul desktop, nu dintr-un terminal fără display.");
        Environment.ExitCode = 1;
        return;
    }
    new DesktopGame(new FactoryGame()).Run();
}
catch (DllNotFoundException ex)
{
    Console.Error.WriteLine($"Lipsește GTK 4. Instalează libgtk-4 înainte de a porni jocul. Detalii: {ex.Message}");
    Environment.ExitCode = 1;
}
catch (EntryPointNotFoundException ex)
{
    Console.Error.WriteLine($"Versiunea GTK instalată nu oferă o funcție necesară jocului. Actualizează GTK 4. Detalii: {ex.Message}");
    Environment.ExitCode = 1;
}

enum TileKind { Empty, IronDeposit, CopperDeposit, Rock, Miner, Belt, Smelter, Assembler, Ship, OilDeposit, Pipe, OilPump, Distillery, ChemicalReactor, Polymerizer, RubberTree, LatexCollector, GlueMixer, TinDeposit, Tree, AlloySmelter, RotorPress }
enum ItemKind { IronOre, CopperOre, IronPlate, CopperPlate, CopperWire, Gear, Circuit, Frame, Motor, AdvancedCircuit, Processor, RocketPart, TinOre, Bronze, Rotor, Pump, Mainframe, Thruster }
enum FluidKind { Petroleum, Ethanol, Ethylene, Polyethylene, Latex, Glue, SolderingAlloy }
enum Direction { North, East, South, West }

sealed record FactoryRecipe(string Id, int Level, string Name, string Product, TileKind Machine,
    ItemKind? Output, int Reward, Dictionary<ItemKind, int> Inputs)
{
    public Dictionary<FluidKind, int> FluidInputs { get; init; } = new();
    public FluidKind? FluidProduct { get; init; }
}

static class RecipeBook
{
    public static readonly FactoryRecipe[] All =
    [
        new("iron_plate", 1, "Placă de fier", "Fe placă", TileKind.Smelter, ItemKind.IronPlate, 12, new() { [ItemKind.IronOre] = 1 }),
        new("copper_plate", 2, "Placă de cupru", "Cu placă", TileKind.Smelter, ItemKind.CopperPlate, 14, new() { [ItemKind.CopperOre] = 1 }),
        new("copper_wire", 3, "Fir de cupru", "Fir Cu", TileKind.Assembler, ItemKind.CopperWire, 16, new() { [ItemKind.CopperPlate] = 1 }),
        new("soldering_alloy", 4, "Aliaj de lipit", "Aliaj de lipit lichid", TileKind.Smelter, null, 0, new() { [ItemKind.TinOre] = 1 })
        { FluidProduct = FluidKind.SolderingAlloy },
        new("iron_gear", 4, "Roată dințată", "Angrenaj", TileKind.Assembler, ItemKind.Gear, 18, new() { [ItemKind.IronPlate] = 2 }),
        new("circuit", 5, "Circuit", "Circuit", TileKind.Assembler, ItemKind.Circuit, 24, new() { [ItemKind.IronPlate] = 1, [ItemKind.CopperWire] = 2 })
        { FluidInputs = new() { [FluidKind.SolderingAlloy] = 1 } },
        new("bronze", 5, "Bronz", "Bronz", TileKind.AlloySmelter, ItemKind.Bronze, 30, new() { [ItemKind.TinOre] = 1, [ItemKind.CopperOre] = 1 }),
        new("rotor", 6, "Rotor", "Rotor", TileKind.RotorPress, ItemKind.Rotor, 32, new() { [ItemKind.IronPlate] = 2 }),
        new("frame", 6, "Cadru ranforsat", "Cadru", TileKind.Assembler, ItemKind.Frame, 28, new() { [ItemKind.IronPlate] = 2, [ItemKind.Gear] = 1 })
        { FluidInputs = new() { [FluidKind.Glue] = 1 } },
        new("motor", 7, "Motor", "Motor", TileKind.Assembler, ItemKind.Motor, 34, new() { [ItemKind.Gear] = 1, [ItemKind.CopperWire] = 1 })
        { FluidInputs = new() { [FluidKind.Polyethylene] = 1 } },
        new("pump_component", 7, "Pump", "Pump", TileKind.Assembler, ItemKind.Pump, 42, new() { [ItemKind.Bronze] = 1, [ItemKind.Rotor] = 1 }),
        new("advanced_circuit", 8, "Circuit avansat", "Circuit+", TileKind.Assembler, ItemKind.AdvancedCircuit, 42, new() { [ItemKind.Circuit] = 1, [ItemKind.CopperPlate] = 1, [ItemKind.CopperWire] = 1 }),
        new("processor", 9, "Procesor", "Procesor", TileKind.Assembler, ItemKind.Processor, 55, new() { [ItemKind.AdvancedCircuit] = 1, [ItemKind.Frame] = 1 })
        { FluidInputs = new() { [FluidKind.SolderingAlloy] = 2 } },
        new("rocket_part", 10, "Piesă de rachetă", "Rachetă", TileKind.Assembler, ItemKind.RocketPart, 75, new() { [ItemKind.Processor] = 1, [ItemKind.Motor] = 1, [ItemKind.Frame] = 1 }),
        new("mainframe", 11, "Mainframe", "Mainframe", TileKind.Assembler, ItemKind.Mainframe, 110, new() { [ItemKind.Processor] = 2, [ItemKind.Frame] = 1 })
        { FluidInputs = new() { [FluidKind.SolderingAlloy] = 1 } },
        new("thruster", 12, "Thruster", "Thruster", TileKind.Assembler, ItemKind.Thruster, 95, new() { [ItemKind.Pump] = 1, [ItemKind.Motor] = 1 })
        { FluidInputs = new() { [FluidKind.Glue] = 1 } }
    ];
    public static int BuildCost(FactoryRecipe recipe) => recipe.Machine switch
    {
        TileKind.Smelter => 30, TileKind.AlloySmelter => 55, TileKind.RotorPress => 50, _ => 45
    };
    public static FactoryRecipe ById(string id) => All.First(r => r.Id == id);
    public static FactoryRecipe? ForOutput(ItemKind item) => All.FirstOrDefault(r => r.Output == item);
}

sealed record FluidRecipe(string Id, string Name, string MachineName, TileKind Machine,
    FluidKind Input, FluidKind Output);

static class FluidRecipeBook
{
    public static readonly FluidRecipe[] All =
    [
        new("petroleum_to_ethanol", "Petrol → Etanol", "Distilerie", TileKind.Distillery, FluidKind.Petroleum, FluidKind.Ethanol),
        new("ethanol_to_ethylene", "Etanol → Etilenă", "Reactor chimic", TileKind.ChemicalReactor, FluidKind.Ethanol, FluidKind.Ethylene),
        new("ethylene_to_polyethylene", "Etilenă → Polietilenă", "Polimerizator", TileKind.Polymerizer, FluidKind.Ethylene, FluidKind.Polyethylene),
        new("latex_to_glue", "Latex → Lipici", "Malaxor de lipici", TileKind.GlueMixer, FluidKind.Latex, FluidKind.Glue)
    ];
    public static FluidRecipe ById(string id) => All.First(r => r.Id == id);
    public static FluidRecipe? ForInput(FluidKind fluid) => All.FirstOrDefault(r => r.Input == fluid);
    public static int SaleValue(FluidKind fluid) => fluid switch
    {
        FluidKind.Petroleum => 8, FluidKind.Ethanol => 16, FluidKind.Ethylene => 20,
        FluidKind.Polyethylene => 65, FluidKind.Latex => 10, FluidKind.SolderingAlloy => 22, _ => 28
    };
    public static string Name(FluidKind fluid) => fluid switch
    {
        FluidKind.Petroleum => "petrol", FluidKind.Ethanol => "etanol",
        FluidKind.Ethylene => "etilenă", FluidKind.Polyethylene => "polietilenă", FluidKind.Latex => "latex", FluidKind.Glue => "lipici", _ => "aliaj de lipit"
    };
}

sealed record DeliveryContract(Dictionary<ItemKind, int> Items, Dictionary<FluidKind, int> Fluids)
{
    public int Total => Items.Values.Sum() + Fluids.Values.Sum();
}

static class ContractBook
{
    static Dictionary<ItemKind, int> Items(params (ItemKind item, int count)[] values) => values.ToDictionary(v => v.item, v => v.count);
    static Dictionary<FluidKind, int> Fluids(params (FluidKind fluid, int count)[] values) => values.ToDictionary(v => v.fluid, v => v.count);

    public static DeliveryContract ForLevel(int level) => level switch
    {
        1 => new(Items((ItemKind.IronPlate, 3)), Fluids()),
        2 => new(Items((ItemKind.IronPlate, 4), (ItemKind.CopperPlate, 5)), Fluids()),
        3 => new(Items((ItemKind.IronPlate, 6), (ItemKind.CopperWire, 8)), Fluids()),
        4 => new(Items((ItemKind.IronPlate, 10), (ItemKind.CopperPlate, 3), (ItemKind.Gear, 10)), Fluids((FluidKind.Ethanol, 5))),
        5 => new(Items((ItemKind.Circuit, 8), (ItemKind.CopperWire, 6)), Fluids((FluidKind.Polyethylene, 5), (FluidKind.SolderingAlloy, 5))),
        6 => new(Items((ItemKind.Frame, 8), (ItemKind.Gear, 6)), Fluids((FluidKind.Polyethylene, 8), (FluidKind.Glue, 5))),
        7 => new(Items((ItemKind.Motor, 6), (ItemKind.Circuit, 8)), Fluids((FluidKind.Glue, 8))),
        8 => new(Items((ItemKind.AdvancedCircuit, 6)), Fluids((FluidKind.Polyethylene, 10), (FluidKind.Ethylene, 5))),
        9 => new(Items((ItemKind.Processor, 4), (ItemKind.Motor, 8)), Fluids((FluidKind.Ethanol, 10), (FluidKind.SolderingAlloy, 8))),
        10 => new(Items((ItemKind.RocketPart, 3), (ItemKind.CopperPlate, 5)), Fluids((FluidKind.Polyethylene, 10), (FluidKind.Glue, 10))),
        11 => new(Items((ItemKind.Mainframe, 2)), Fluids((FluidKind.SolderingAlloy, 5))),
        12 => new(Items((ItemKind.Thruster, 3), (ItemKind.Pump, 3)), Fluids((FluidKind.Glue, 5))),
        _ => new(Items(), Fluids())
    };
}

sealed class Tile
{
    public TileKind Kind;
    public Direction Direction;
    public ItemKind? Ore;
    public int Progress;
    public Dictionary<ItemKind, int> Stock = new();
    public Dictionary<ItemKind, int> Output = new();
    public Dictionary<FluidKind, int> FluidStock = new();
    public ItemKind? BeltItem;
    public FluidKind? PipeFluid;
    public FluidKind? FluidInput;
    public FluidKind? FluidOutput;
    public string? RecipeId;
    public string? FluidRecipeId;
    public bool PlayerBuilt;
    public FactoryRecipe? InstalledRecipe => RecipeId is null ? null : RecipeBook.ById(RecipeId);
    public FluidRecipe? InstalledFluidRecipe => FluidRecipeId is null ? null : FluidRecipeBook.ById(FluidRecipeId);
    public int Stored => Stock.Values.Sum() + Output.Values.Sum() + FluidStock.Values.Sum() + (PipeFluid is null ? 0 : 1) + (FluidInput is null ? 0 : 1) + (FluidOutput is null ? 0 : 1);
}

sealed class SavedCoordinate
{
    public int X { get; set; }
    public int Y { get; set; }
}

sealed class SavedTile
{
    public int X { get; set; }
    public int Y { get; set; }
    public TileKind Kind { get; set; }
    public Direction Direction { get; set; }
    public ItemKind? Ore { get; set; }
    public int Progress { get; set; }
    public Dictionary<ItemKind, int> Stock { get; set; } = new();
    public Dictionary<ItemKind, int> Output { get; set; } = new();
    public Dictionary<FluidKind, int> FluidStock { get; set; } = new();
    public ItemKind? BeltItem { get; set; }
    public FluidKind? PipeFluid { get; set; }
    public FluidKind? FluidInput { get; set; }
    public FluidKind? FluidOutput { get; set; }
    public string? RecipeId { get; set; }
    public string? FluidRecipeId { get; set; }
    public bool PlayerBuilt { get; set; }
}

sealed class FactoryGameData
{
    public int Seed { get; set; }
    public int Money { get; set; }
    public int Delivered { get; set; }
    public int Tick { get; set; }
    public int Level { get; set; }
    public int LevelProgress { get; set; }
    public Dictionary<ItemKind, int> ContractItems { get; set; } = new();
    public Dictionary<FluidKind, int> ContractFluids { get; set; } = new();
    public int LastIncome { get; set; }
    public string LastSales { get; set; } = "";
    public List<SavedCoordinate> GeneratedChunks { get; set; } = new();
    public List<SavedCoordinate> Buildings { get; set; } = new();
    public List<SavedTile> Tiles { get; set; } = new();
}

sealed class SaveFileData
{
    public int Version { get; set; } = 1;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public double CameraX { get; set; }
    public double CameraY { get; set; }
    public FactoryGameData Game { get; set; } = new();
}

sealed class FactoryGame
{
    public const int Width = 16, Height = 10, MaxLevel = 12;
    readonly Dictionary<(int x, int y), Tile> map = new();
    readonly HashSet<(int x, int y)> generatedChunks = new();
    readonly HashSet<(int x, int y)> buildings = new();
    readonly Dictionary<ItemKind, int> ContractItems = new();
    readonly Dictionary<FluidKind, int> ContractFluids = new();
    public int Money { get; private set; }
    public int Delivered { get; private set; }
    public int Tick { get; private set; }
    public int Seed { get; private set; }
    public int Level { get; private set; }
    public int LevelProgress { get; private set; }
    public DeliveryContract CurrentContract => ContractBook.ForLevel(Level);
    public int LastIncome { get; private set; }
    public string LastSales { get; private set; } = "";
    public IReadOnlyList<FactoryRecipe> UnlockedRecipes => RecipeBook.All.Where(r => r.Level <= Level).OrderBy(r => r.Level).ToArray();

    public FactoryGame() => Reset();

    public FactoryGameData ExportSaveData() => new()
    {
        Seed = Seed, Money = Money, Delivered = Delivered, Tick = Tick, Level = Level,
        LevelProgress = LevelProgress, ContractItems = new(ContractItems), ContractFluids = new(ContractFluids),
        LastIncome = LastIncome, LastSales = LastSales,
        GeneratedChunks = generatedChunks.Select(c => new SavedCoordinate { X = c.x, Y = c.y }).ToList(),
        Buildings = buildings.Select(c => new SavedCoordinate { X = c.x, Y = c.y }).ToList(),
        Tiles = map.Select(entry => new SavedTile
        {
            X = entry.Key.x, Y = entry.Key.y, Kind = entry.Value.Kind, Direction = entry.Value.Direction,
            Ore = entry.Value.Ore, Progress = entry.Value.Progress, Stock = new(entry.Value.Stock),
            Output = new(entry.Value.Output), FluidStock = new(entry.Value.FluidStock), BeltItem = entry.Value.BeltItem, RecipeId = entry.Value.RecipeId,
            PipeFluid = entry.Value.PipeFluid, FluidInput = entry.Value.FluidInput, FluidOutput = entry.Value.FluidOutput,
            FluidRecipeId = entry.Value.FluidRecipeId,
            PlayerBuilt = entry.Value.PlayerBuilt
        }).ToList()
    };

    public static FactoryGame FromSaveData(FactoryGameData data)
    {
        var game = new FactoryGame();
        game.map.Clear(); game.generatedChunks.Clear(); game.buildings.Clear();
        foreach (var c in data.GeneratedChunks) game.generatedChunks.Add((c.X, c.Y));
        foreach (var c in data.Buildings) game.buildings.Add((c.X, c.Y));
        foreach (var saved in data.Tiles)
            game.map[(saved.X, saved.Y)] = new Tile
            {
                Kind = saved.Kind, Direction = saved.Direction, Ore = saved.Ore, Progress = saved.Progress,
                Stock = saved.Stock ?? new(), Output = saved.Output ?? new(), FluidStock = saved.FluidStock ?? new(), BeltItem = saved.BeltItem,
                PipeFluid = saved.PipeFluid, FluidInput = saved.FluidInput, FluidOutput = saved.FluidOutput,
                RecipeId = saved.RecipeId, FluidRecipeId = saved.FluidRecipeId, PlayerBuilt = saved.PlayerBuilt
            };
        game.Seed = data.Seed; game.Money = data.Money; game.Delivered = data.Delivered;
        game.Tick = data.Tick; game.Level = data.Level; game.LevelProgress = data.LevelProgress;
        game.ContractItems.Clear(); game.ContractFluids.Clear();
        foreach (var (item, count) in data.ContractItems ?? new()) game.ContractItems[item] = count;
        foreach (var (fluid, count) in data.ContractFluids ?? new()) game.ContractFluids[fluid] = count;
        if (game.ContractItems.Count == 0 && game.ContractFluids.Count == 0 && game.LevelProgress > 0 && game.Level < MaxLevel)
        {
            var legacyItem = game.CurrentContract.Items.FirstOrDefault();
            if (!legacyItem.Equals(default(KeyValuePair<ItemKind, int>)))
                game.ContractItems[legacyItem.Key] = Math.Min(game.LevelProgress, legacyItem.Value);
            game.LevelProgress = game.ContractItems.Values.Sum();
        }
        game.LastIncome = data.LastIncome; game.LastSales = data.LastSales ?? "";
        return game;
    }

    public void Reset()
    {
        map.Clear(); generatedChunks.Clear(); buildings.Clear();
        Seed = RandomNumberGenerator.GetInt32(100_000, 1_000_000);
        EnsureChunkGenerated(0, 0);
        Money = 250; Delivered = 0; Tick = 0; Level = 1; LevelProgress = 0; ContractItems.Clear(); ContractFluids.Clear(); LastIncome = 0; LastSales = "";
    }

    public void EnsureChunkGenerated(int chunkX, int chunkY)
    {
        if (!generatedChunks.Add((chunkX, chunkY))) return;
        var chunkSeed = unchecked(Seed ^ (chunkX * 73_856_093) ^ (chunkY * 19_349_663));
        var random = new Random(chunkSeed);
        if (chunkX == 0 && chunkY == 0)
        {
            var shipY = random.Next(1, Height - 1);
            map[(13, shipY)] = new Tile { Kind = TileKind.Ship };
        }
        var candidates = (from x in Enumerable.Range(1, 10) from y in Enumerable.Range(1, Height - 2)
            select (x: chunkX * Width + x, y: chunkY * Height + y)).ToList();
        Shuffle(candidates, random);
        var veins = new List<(int x, int y)>();
        foreach (var point in candidates)
        {
            if (veins.All(v => Math.Abs(v.x - point.x) + Math.Abs(v.y - point.y) >= 3)) veins.Add(point);
            if (veins.Count == 4) break;
        }
        var ores = new[] { TileKind.IronDeposit, TileKind.IronDeposit, TileKind.CopperDeposit, TileKind.CopperDeposit };
        Shuffle(ores, random);
        for (var i = 0; i < veins.Count; i++) map[veins[i]] = new Tile { Kind = ores[i] };

        var tinSite = candidates.FirstOrDefault(point => !map.ContainsKey(point)
            && veins.All(vein => Math.Abs(vein.x - point.x) + Math.Abs(vein.y - point.y) >= 2));
        if (tinSite != default) map[tinSite] = new Tile { Kind = TileKind.TinDeposit };

        var oilSite = candidates.FirstOrDefault(point => !map.ContainsKey(point)
            && veins.All(vein => Math.Abs(vein.x - point.x) + Math.Abs(vein.y - point.y) >= 2));
        if (oilSite != default) map[oilSite] = new Tile { Kind = TileKind.OilDeposit };
        var rubberSite = candidates.FirstOrDefault(point => !map.ContainsKey(point)
            && veins.All(vein => Math.Abs(vein.x - point.x) + Math.Abs(vein.y - point.y) >= 2)
            && (oilSite == default || Math.Abs(oilSite.x - point.x) + Math.Abs(oilSite.y - point.y) >= 3));
        if (rubberSite != default) map[rubberSite] = new Tile { Kind = TileKind.RubberTree };

        var rocks = (from x in Enumerable.Range(1, 12) from y in Enumerable.Range(0, Height)
            select (x: chunkX * Width + x, y: chunkY * Height + y))
            .Where(p => !map.ContainsKey(p)
                && veins.All(v => Math.Abs(v.x - p.x) + Math.Abs(v.y - p.y) > 1)).ToList();
        Shuffle(rocks, random);
        foreach (var point in rocks.Take(7)) map[point] = new Tile { Kind = TileKind.Rock };
        foreach (var point in rocks.Skip(7).Take(7)) map[point] = new Tile { Kind = TileKind.Tree };
    }

    public Tile At(int x, int y)
    {
        EnsureChunkGenerated(FloorDiv(x, Width), FloorDiv(y, Height));
        return map.TryGetValue((x, y), out var tile) ? tile : Empty;
    }
    static readonly Tile Empty = new();

    public string Build(string kind, int x, int y, ItemKind ore, Direction direction, string recipeId, string fluidRecipeId)
    {
        var tile = At(x, y); if (tile.Kind != TileKind.Empty) return $"Locul ({x}, {y}) este ocupat: {KindName(tile.Kind)}.";
        var recipe = kind == "machine" ? RecipeBook.ById(recipeId) : null;
        var fluidRecipe = kind == "fluid_machine" ? FluidRecipeBook.ById(fluidRecipeId) : null;
        var cost = kind switch
        {
            "miner" => 25, "belt" => 2, "pipe" => 2, "pump" => 90, "latex_collector" => 80, "machine" => RecipeBook.BuildCost(recipe!),
            "fluid_machine" => fluidRecipe!.Machine == TileKind.Distillery ? 70 : fluidRecipe.Machine == TileKind.ChemicalReactor ? 90 : 100,
            "ship" => 120, _ => 0
        };
        if (cost == 0) return "Clădire necunoscută.";
        if (kind == "miner")
        {
            var deposit = ore switch
            {
                ItemKind.IronOre => TileKind.IronDeposit,
                ItemKind.CopperOre => TileKind.CopperDeposit,
                _ => TileKind.TinDeposit
            };
            if (!Neighbors(x, y).Any(p => At(p.x, p.y).Kind == deposit)) return "Minerul trebuie pus lângă zăcământul potrivit.";
        }
        if (kind == "pump" && !Neighbors(x, y).Any(p => At(p.x, p.y).Kind == TileKind.OilDeposit))
            return "Pompa trebuie construită lângă un zăcământ de petrol.";
        if (kind == "latex_collector" && !Neighbors(x, y).Any(p => At(p.x, p.y).Kind == TileKind.RubberTree))
            return "Colectorul de latex trebuie construit lângă un arbore de cauciuc.";
        if (Money < cost) return $"Fonduri insuficiente. Costul este ${cost}.";
        Money -= cost;
        tile = new Tile { Kind = kind switch
        {
            "miner" => TileKind.Miner, "belt" => TileKind.Belt, "pipe" => TileKind.Pipe, "pump" => TileKind.OilPump, "latex_collector" => TileKind.LatexCollector,
            "machine" => recipe!.Machine, "fluid_machine" => fluidRecipe!.Machine, "ship" => TileKind.Ship, _ => TileKind.Empty
        } };
        if (kind == "miner") tile.Ore = ore;
        if (kind is "belt" or "pipe" or "pump" or "latex_collector") tile.Direction = direction;
        if (kind == "machine") tile.RecipeId = recipe!.Id;
        if (kind == "fluid_machine") tile.FluidRecipeId = fluidRecipe!.Id;
        if (kind == "ship") tile.PlayerBuilt = true;
        map[(x, y)] = tile;
        buildings.Add((x, y));
        if (kind == "ship") return $"Navă cargo nouă construită la ({x}, {y}).";
        if (kind == "fluid_machine") return $"Construită {fluidRecipe!.MachineName} pentru {fluidRecipe.Name} la ({x}, {y}).";
        if (kind == "pump") return $"Construită pompă de petrol la ({x}, {y}).";
        if (kind == "latex_collector") return $"Construit colector de latex la ({x}, {y}).";
        return kind == "machine" ? $"Construit {recipe!.Name} la ({x}, {y})." : $"Construit: {KindName(tile.Kind)} la ({x}, {y}).";
    }

    public string Delete(int x, int y)
    {
        var tile = At(x, y);
        if (tile.Kind is not (TileKind.Miner or TileKind.Belt or TileKind.Pipe or TileKind.OilPump or TileKind.LatexCollector or TileKind.Smelter or TileKind.Assembler or TileKind.AlloySmelter or TileKind.RotorPress or TileKind.Distillery or TileKind.ChemicalReactor or TileKind.Polymerizer or TileKind.GlueMixer) && !(tile.Kind == TileKind.Ship && tile.PlayerBuilt))
            return tile.Kind == TileKind.Empty ? "Celula este deja liberă." : "Zăcămintele, bolovanii, copacii și nava inițială nu pot fi demolate.";
        var name = KindName(tile.Kind);
        map.Remove((x, y));
        buildings.Remove((x, y));
        return $"Demolat {name} la ({x}, {y}); obiectele din el au fost șterse.";
    }

    public void Advance()
    {
        Tick++; LastIncome = 0; var sales = new List<string>();
        var moves = new List<(Tile From, Tile To, ItemKind Item, bool Ship)>();
        foreach (var coords in buildings.ToArray())
        {
            if (!map.TryGetValue(coords, out var belt)) continue;
            if (belt.Kind != TileKind.Belt || belt.BeltItem is not { } item) continue;
            var (nx, ny) = Next(coords.x, coords.y, belt.Direction);
            var dest = At(nx, ny);
            if (dest.Kind == TileKind.Belt && dest.BeltItem is null) moves.Add((belt, dest, item, false));
            else if (dest.InstalledRecipe is { } inputRecipe && inputRecipe.Inputs.ContainsKey(item)) moves.Add((belt, dest, item, false));
            else if (dest.Kind == TileKind.Ship && RecipeBook.ForOutput(item) is not null) moves.Add((belt, dest, item, true));
        }
        foreach (var move in moves)
        {
            if (move.From.BeltItem != move.Item) continue;
            if (!move.Ship && move.To.Kind == TileKind.Belt && move.To.BeltItem is not null) continue;
            move.From.BeltItem = null;
            if (move.Ship)
            {
                var shippedRecipe = RecipeBook.ForOutput(move.Item)!;
                RegisterDelivery(move.Item, null, shippedRecipe.Reward, shippedRecipe.Name, sales);
            }
            else if (move.To.Kind == TileKind.Belt) move.To.BeltItem = move.Item;
            else Add(move.To.Stock, move.Item);
        }
        foreach (var coords in buildings.ToArray())
        {
            if (!map.TryGetValue(coords, out var tile)) continue;
            if (tile.Kind == TileKind.Miner && ++tile.Progress >= 2) { tile.Progress = 0; Add(tile.Output, tile.Ore!.Value); }
            else if (tile.InstalledRecipe is { } recipe
                && (recipe.FluidProduct is null || tile.FluidOutput is null))
            {
                if (recipe.Inputs.All(input => tile.Stock.GetValueOrDefault(input.Key) >= input.Value)
                    && recipe.FluidInputs.All(input => tile.FluidStock.GetValueOrDefault(input.Key) >= input.Value))
                {
                    foreach (var input in recipe.Inputs) for (var i = 0; i < input.Value; i++) Take(tile.Stock, input.Key);
                    foreach (var input in recipe.FluidInputs) for (var i = 0; i < input.Value; i++) Take(tile.FluidStock, input.Key);
                    if (recipe.FluidProduct is { } fluidProduct) tile.FluidOutput = fluidProduct;
                    else if (recipe.Output is { } itemProduct) Add(tile.Output, itemProduct);
                }
            }
            if (tile.Output.Count == 0) continue;
            var itemToSend = tile.Output.First().Key;
            foreach (var (bx, by) in Neighbors(coords.x, coords.y))
            {
                var belt = At(bx, by);
                // A belt aimed back into this building is an input belt. Never
                // inject finished goods onto it, or they will loop into the machine.
                if (belt.Kind == TileKind.Belt && belt.BeltItem is null
                    && Next(bx, by, belt.Direction) != coords)
                { belt.BeltItem = itemToSend; Take(tile.Output, itemToSend); break; }
            }
        }
        AdvanceFluids(sales);
        LastSales = string.Join(", ", sales);
    }

    void AdvanceFluids(List<string> sales)
    {
        var moves = new List<(Tile From, Tile To, FluidKind Fluid, bool Ship)>();
        foreach (var coords in buildings.ToArray())
        {
            if (!map.TryGetValue(coords, out var pipe) || pipe.Kind != TileKind.Pipe || pipe.PipeFluid is not { } fluid) continue;
            var (nx, ny) = Next(coords.x, coords.y, pipe.Direction);
            var destination = At(nx, ny);
            if (destination.Kind == TileKind.Pipe && destination.PipeFluid is null)
                moves.Add((pipe, destination, fluid, false));
            else if (destination.InstalledFluidRecipe is { } recipe && recipe.Input == fluid && destination.FluidInput is null)
                moves.Add((pipe, destination, fluid, false));
            else if (destination.InstalledRecipe is { } solidRecipe
                && solidRecipe.FluidInputs.ContainsKey(fluid)
                && destination.FluidStock.GetValueOrDefault(fluid) < solidRecipe.FluidInputs[fluid])
                moves.Add((pipe, destination, fluid, false));
            else if (destination.Kind == TileKind.Ship && FluidRecipeBook.SaleValue(fluid) > 0)
                moves.Add((pipe, destination, fluid, true));
        }

        foreach (var move in moves)
        {
            if (move.From.PipeFluid != move.Fluid) continue;
            if (move.Ship)
            {
                move.From.PipeFluid = null;
                var reward = FluidRecipeBook.SaleValue(move.Fluid);
                RegisterDelivery(null, move.Fluid, reward, FluidRecipeBook.Name(move.Fluid), sales);
            }
            else if (move.To.Kind == TileKind.Pipe && move.To.PipeFluid is null)
            {
                move.From.PipeFluid = null; move.To.PipeFluid = move.Fluid;
            }
            else if (move.To.InstalledFluidRecipe is not null && move.To.FluidInput is null)
            {
                move.From.PipeFluid = null; move.To.FluidInput = move.Fluid;
            }
            else if (move.To.InstalledRecipe is { } solidRecipe
                && solidRecipe.FluidInputs.ContainsKey(move.Fluid)
                && move.To.FluidStock.GetValueOrDefault(move.Fluid) < solidRecipe.FluidInputs[move.Fluid])
            {
                move.From.PipeFluid = null;
                move.To.FluidStock[move.Fluid] = move.To.FluidStock.GetValueOrDefault(move.Fluid) + 1;
            }
        }

        foreach (var coords in buildings.ToArray())
        {
            if (!map.TryGetValue(coords, out var pump) || pump.Kind is not (TileKind.OilPump or TileKind.LatexCollector)) continue;
            var (nx, ny) = Next(coords.x, coords.y, pump.Direction);
            var pipe = At(nx, ny);
            if (pipe.Kind == TileKind.Pipe && pipe.PipeFluid is null)
                pipe.PipeFluid = pump.Kind == TileKind.OilPump ? FluidKind.Petroleum : FluidKind.Latex;
        }

        foreach (var coords in buildings.ToArray())
        {
            if (!map.TryGetValue(coords, out var machine)) continue;
            if (machine.InstalledRecipe is { FluidProduct: not null } && machine.FluidOutput is { } smeltOutput)
            {
                foreach (var (px, py) in Neighbors(coords.x, coords.y))
                {
                    var pipe = At(px, py);
                    if (pipe.Kind != TileKind.Pipe || pipe.PipeFluid is not null || Next(px, py, pipe.Direction) == coords) continue;
                    pipe.PipeFluid = smeltOutput; machine.FluidOutput = null; break;
                }
            }
            if (machine.InstalledFluidRecipe is not { } recipe) continue;
            if (machine.FluidOutput is { } output)
            {
                foreach (var (px, py) in Neighbors(coords.x, coords.y))
                {
                    var pipe = At(px, py);
                    if (pipe.Kind != TileKind.Pipe || pipe.PipeFluid is not null || Next(px, py, pipe.Direction) == coords) continue;
                    pipe.PipeFluid = output; machine.FluidOutput = null; break;
                }
            }
            if (machine.FluidOutput is null && machine.FluidInput == recipe.Input)
            {
                machine.FluidInput = null;
                machine.FluidOutput = recipe.Output;
            }
        }
    }

    void RegisterDelivery(ItemKind? item, FluidKind? fluid, int reward, string name, List<string> sales)
    {
        Delivered++; Money += reward; LastIncome += reward; sales.Add(name);
        if (Level >= MaxLevel) return;
        var contract = CurrentContract;
        if (item is { } deliveredItem && contract.Items.TryGetValue(deliveredItem, out var itemTarget))
            ContractItems[deliveredItem] = Math.Min(itemTarget, ContractItems.GetValueOrDefault(deliveredItem) + 1);
        if (fluid is { } deliveredFluid && contract.Fluids.TryGetValue(deliveredFluid, out var fluidTarget))
            ContractFluids[deliveredFluid] = Math.Min(fluidTarget, ContractFluids.GetValueOrDefault(deliveredFluid) + 1);
        LevelProgress = ContractItems.Values.Sum() + ContractFluids.Values.Sum();
        if (LevelProgress >= contract.Total)
        {
            Level++;
            ContractItems.Clear(); ContractFluids.Clear(); LevelProgress = 0;
        }
    }

    static bool Take(Dictionary<ItemKind, int> stock, ItemKind item)
    { if (stock.GetValueOrDefault(item) <= 0) return false; stock[item]--; if (stock[item] == 0) stock.Remove(item); return true; }
    static bool Take(Dictionary<FluidKind, int> stock, FluidKind fluid)
    { if (stock.GetValueOrDefault(fluid) <= 0) return false; stock[fluid]--; if (stock[fluid] == 0) stock.Remove(fluid); return true; }
    static void Add(Dictionary<ItemKind, int> stock, ItemKind item) => stock[item] = stock.GetValueOrDefault(item) + 1;
    static void Shuffle<T>(IList<T> items, Random random)
    { for (var i = items.Count - 1; i > 0; i--) { var j = random.Next(i + 1); (items[i], items[j]) = (items[j], items[i]); } }
    static int FloorDiv(int n, int d) => n >= 0 ? n / d : (n - d + 1) / d;
    static (int x, int y) Next(int x, int y, Direction d) => d switch { Direction.North => (x, y - 1), Direction.East => (x + 1, y), Direction.South => (x, y + 1), _ => (x - 1, y) };
    static IEnumerable<(int x, int y)> Neighbors(int x, int y)
    { foreach (Direction d in Enum.GetValues<Direction>()) yield return Next(x, y, d); }
    static string KindName(TileKind kind) => kind switch
    {
        TileKind.IronDeposit => "zăcământ de fier", TileKind.CopperDeposit => "zăcământ de cupru", TileKind.Rock => "bolovan",
        TileKind.TinDeposit => "zăcământ de staniu", TileKind.Tree => "copac",
        TileKind.Miner => "miner", TileKind.Belt => "bandă", TileKind.Smelter => "topitor", TileKind.Assembler => "asamblor",
        TileKind.AlloySmelter => "topitor de aliaje", TileKind.RotorPress => "Rotor Press", TileKind.Ship => "navă cargo",
        TileKind.OilDeposit => "zăcământ de petrol", TileKind.Pipe => "țeavă", TileKind.OilPump => "pompă de petrol",
        TileKind.RubberTree => "arbore de cauciuc", TileKind.LatexCollector => "colector de latex",
        TileKind.Distillery => "distilerie", TileKind.ChemicalReactor => "reactor chimic", TileKind.Polymerizer => "polimerizator", TileKind.GlueMixer => "malaxor de lipici", _ => "loc liber"
    };
}

static class SaveFileStore
{
    static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string SaveDirectory
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "FactoryBuilder.csproj")))
                    return Path.Combine(directory.FullName, "saves");
                directory = directory.Parent;
            }
            return Path.Combine(AppContext.BaseDirectory, "saves");
        }
    }

    public static List<(string Path, SaveFileData Data)> List()
    {
        Directory.CreateDirectory(SaveDirectory);
        var saves = new List<(string Path, SaveFileData Data)>();
        foreach (var path in Directory.GetFiles(SaveDirectory, "*.json"))
        {
            try
            {
                var data = JsonSerializer.Deserialize<SaveFileData>(File.ReadAllText(path));
                if (data is not null && data.Version == 1 && data.Game is not null) saves.Add((path, data));
            }
            catch (JsonException) { }
            catch (IOException) { }
        }
        return saves.OrderByDescending(s => s.Data.UpdatedUtc).ToList();
    }

    public static string Create(string name, FactoryGame game)
    {
        Directory.CreateDirectory(SaveDirectory);
        var data = new SaveFileData { Name = name.Trim(), Game = game.ExportSaveData() };
        var path = Path.Combine(SaveDirectory, data.Id + ".json");
        Write(path, data);
        return path;
    }

    public static (FactoryGame Game, SaveFileData Data) Load(string path)
    {
        var data = JsonSerializer.Deserialize<SaveFileData>(File.ReadAllText(path))
            ?? throw new InvalidDataException("Fișierul de salvare este gol sau invalid.");
        if (data.Version != 1 || data.Game is null) throw new InvalidDataException("Versiunea fișierului de salvare nu este acceptată.");
        return (FactoryGame.FromSaveData(data.Game), data);
    }

    public static void Save(string path, string name, FactoryGame game, double cameraX, double cameraY)
    {
        var data = File.Exists(path)
            ? JsonSerializer.Deserialize<SaveFileData>(File.ReadAllText(path)) ?? new SaveFileData()
            : new SaveFileData();
        data.Name = name; data.Game = game.ExportSaveData(); data.CameraX = cameraX; data.CameraY = cameraY; data.UpdatedUtc = DateTime.UtcNow;
        Write(path, data);
    }

    public static void Rename(string path, string name)
    {
        var data = JsonSerializer.Deserialize<SaveFileData>(File.ReadAllText(path))
            ?? throw new InvalidDataException("Fișierul de salvare este invalid.");
        data.Name = name.Trim(); data.UpdatedUtc = DateTime.UtcNow;
        Write(path, data);
    }

    public static void Delete(string path) => File.Delete(path);

    static void Write(string path, SaveFileData data)
    {
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(data, JsonOptions));
        File.Move(temporary, path, true);
    }
}

sealed class DesktopGame
{
    const string Gtk = "libgtk-4.so.1", GObject = "libgobject-2.0.so.0", Gio = "libgio-2.0.so.0";
    const uint CssPriority = 600;
    static readonly Dictionary<IntPtr, Action> Actions = new();
    static readonly SignalCallback ClickCallback = OnClicked;
    static readonly KeyCallback KeyboardCallback = OnKey;
    static readonly KeyReleaseCallback KeyboardReleaseCallback = OnKeyReleased;
    static readonly CloseCallback CloseWindowCallback = OnClose;
    static readonly TimerCallback TimeCallback = OnTimer;
    static readonly TimerCallback SpriteFrameCallback = OnSpriteFrameTimer;
    static readonly TimerCallback BeltMotionCallback = OnBeltMotionTimer;
    static readonly NotifyCallback MusicEndedCallback = OnMusicEnded;
    static readonly SignalCallback AdjustmentCallback = OnAdjustmentChanged;
    static readonly NotifyCallback ViewportNotifyCallback = OnViewportNotify;
    static readonly NotifyCallback WindowSizeNotifyCallback = OnWindowSizeNotify;
    static readonly IdleCallback ScrollRestoreCallback = OnScrollRestore;
    static readonly IdleCallback ResizeRestoreCallback = OnResizeRestore;
    static DesktopGame? current;
    static IntPtr mainLoop;
    FactoryGame game;
    readonly Dictionary<(int x, int y), IntPtr> cells = new();
    readonly Dictionary<(int x, int y), IntPtr> cellImages = new();
    readonly Dictionary<(int x, int y), IntPtr> cellOverlays = new();
    readonly Dictionary<(int x, int y), IntPtr> beltItemImages = new();
    readonly Dictionary<(int x, int y), ItemKind> beltItemKinds = new();
    readonly Dictionary<(int x, int y), IntPtr> pipeFluidImages = new();
    readonly Dictionary<(int x, int y), FluidKind> pipeFluidKinds = new();
    readonly Dictionary<(int x, int y), string[]> cellSpriteFrames = new();
    readonly Dictionary<(int x, int y), int> cellSpriteFrameIndices = new();
    readonly Dictionary<IntPtr, (double width, double height)> responsiveSizes = new();
    readonly Dictionary<IntPtr, double> responsiveGaps = new();
    readonly Dictionary<IntPtr, double> responsiveMargins = new();
    static readonly Dictionary<string, string[]> SpriteFrameCache = new();
    readonly Dictionary<string, IntPtr> buildButtons = new();
    readonly HashSet<IntPtr> viewActions = new();
    readonly HashSet<uint> pressedMovementKeys = new();
    IntPtr window, statusLabel, moneyLabel, tickLabel, levelLabel, deliveryLabel, progressBar, progressHeadingLabel;
    IntPtr seedLabel, worldNameLabel, saveNameEntry, menuStatusLabel, saveListBox, musicToggleButton, musicStream;
    string[] musicTracks = [];
    int musicTrackIndex = -1;
    IntPtr mapGrid, mapScroll, horizontalAdjustment, verticalAdjustment;
    int minChunkX = -1, minChunkY = -1, maxChunkX = 1, maxChunkY = 1;
    int tileSize;
    uint resizeTimerId;
    uint scrollRestoreTimerId;
    uint spriteTimerId;
    uint beltMotionTimerId;
    double beltMotionProgress;
    bool extendingWorld, centerPending, scrollRestorePending, restoringAdjustments;
    double pendingScrollX, pendingScrollY;
    double cameraWorldX, cameraWorldY;
    IntPtr optionsMiner, optionsBelt, directionHeading, oreIronButton, oreCopperButton, oreTinButton;
    string? mode;
    ItemKind ore = ItemKind.IronOre;
    Direction direction = Direction.East;
    string selectedRecipeId = RecipeBook.All[0].Id;
    string selectedFluidRecipeId = FluidRecipeBook.All[0].Id;
    IntPtr autoButton;
    uint timerId;
    string? activeSavePath, activeSaveName, pendingRenamePath;
    bool autoRunning;
    bool musicEnabled = true;

    public DesktopGame(FactoryGame game) { this.game = game; current = this; }
    public void Run()
    {
        InstallTheme();
        mainLoop = GtkNative.g_main_loop_new(IntPtr.Zero, false);
        window = GtkNative.gtk_window_new(); GtkNative.gtk_window_set_title(window, "Factory Frontier");
        GtkNative.g_signal_connect_data(window, "notify::width", Marshal.GetFunctionPointerForDelegate(WindowSizeNotifyCallback), IntPtr.Zero, IntPtr.Zero, 0);
        GtkNative.g_signal_connect_data(window, "notify::height", Marshal.GetFunctionPointerForDelegate(WindowSizeNotifyCallback), IntPtr.Zero, IntPtr.Zero, 0);
        GtkNative.g_signal_connect_data(window, "close-request", Marshal.GetFunctionPointerForDelegate(CloseWindowCallback), IntPtr.Zero, IntPtr.Zero, 0);
        var keys = GtkNative.gtk_event_controller_key_new();
        GtkNative.g_signal_connect_data(keys, "key-pressed", Marshal.GetFunctionPointerForDelegate(KeyboardCallback), IntPtr.Zero, IntPtr.Zero, 0);
        GtkNative.g_signal_connect_data(keys, "key-released", Marshal.GetFunctionPointerForDelegate(KeyboardReleaseCallback), IntPtr.Zero, IntPtr.Zero, 0);
        GtkNative.gtk_widget_add_controller(window, keys);
        GtkNative.gtk_window_set_child(window, BuildMainMenu()); GtkNative.gtk_window_present(window); GtkNative.gtk_window_maximize(window);
        StartBackgroundMusic();
        GtkNative.g_main_loop_run(mainLoop);
        GtkNative.g_main_loop_unref(mainLoop);
    }

    IntPtr BuildMainMenu(string? message = null)
    {
        var root = Box(1, 1.8); Margins(root, 2.0);
        var heading = Box(0, 1.2); var headingText = Box(1, 0.5);
        Append(headingText, Label("⚙  FACTORY FRONTIER", "brand"));
        Append(headingText, Label("Meniu principal · gestionează salvările fabricii", "title"));
        GtkNative.gtk_widget_set_hexpand(headingText, true); Append(heading, headingText);
        musicToggleButton = Button(""); Connect(musicToggleButton, ToggleMusic); UpdateMusicButton(); Append(heading, musicToggleButton);
        var exit = Button("⏻  Ieși pe desktop", "danger");
        Connect(exit, () => GtkNative.gtk_window_close(window)); Append(heading, exit);
        Append(root, heading);

        var createCard = Box(1, 1); GtkNative.gtk_widget_add_css_class(createCard, "card");
        Append(createCard, Label("SALVARE NOUĂ", "accent muted"));
        var createRow = Box(0, 1);
        saveNameEntry = GtkNative.gtk_entry_new();
        GtkNative.gtk_entry_set_placeholder_text(saveNameEntry, "Numele salvării");
        GtkNative.gtk_widget_set_hexpand(saveNameEntry, true);
        RequestResponsiveSize(saveNameEntry, 22, 5);
        Append(createRow, saveNameEntry);
        var create = Button("＋  Joc nou"); Connect(create, CreateNewSave); Append(createRow, create);
        var rename = Button("Redenumește salvarea selectată"); Connect(rename, RenameSelectedSave); Append(createRow, rename);
        Append(createCard, createRow); Append(root, createCard);

        menuStatusLabel = Label(message ?? "Alege o salvare existentă sau creează o lume nouă.", "muted");
        GtkNative.gtk_label_set_wrap(menuStatusLabel, true); Append(root, menuStatusLabel);
        Append(root, Label("SALVĂRI EXISTENTE", "accent muted"));
        var scroll = GtkNative.gtk_scrolled_window_new(); GtkNative.gtk_scrolled_window_set_policy(scroll, 2, 1);
        GtkNative.gtk_widget_set_vexpand(scroll, true); RequestResponsiveSize(scroll, 0, 38);
        saveListBox = Box(1, 0.8); GtkNative.gtk_scrolled_window_set_child(scroll, saveListBox); Append(root, scroll);

        var saves = SaveFileStore.List();
        if (saves.Count == 0) Append(saveListBox, Label("Nu există salvări încă.", "muted"));
        foreach (var (path, data) in saves)
        {
            var row = Box(0, 0.8); GtkNative.gtk_widget_add_css_class(row, "card");
            var details = Box(1, 0.3);
            Append(details, Label(data.Name, "value"));
            Append(details, Label($"Actualizată: {data.UpdatedUtc.ToLocalTime():dd.MM.yyyy HH:mm} · Nivel {data.Game.Level} · Tură {data.Game.Tick}", "muted"));
            GtkNative.gtk_widget_set_hexpand(details, true); Append(row, details);
            var load = Button("Încarcă"); Connect(load, () => LoadSave(path)); Append(row, load);
            var edit = Button("Redenumește"); Connect(edit, () => BeginRename(path, data.Name)); Append(row, edit);
            var delete = Button("Șterge", "danger"); Connect(delete, () => DeleteSave(path)); Append(row, delete);
            Append(saveListBox, row);
        }
        return root;
    }

    void CreateNewSave()
    {
        var name = ReadSaveName();
        if (name.Length == 0) { SetMenuMessage("Introdu un nume pentru salvare."); return; }
        try
        {
            var freshGame = new FactoryGame();
            var path = SaveFileStore.Create(name, freshGame);
            StartGame(freshGame, path, name);
        }
        catch (Exception ex) { SetMenuMessage($"Nu am putut crea salvarea: {ex.Message}"); }
    }

    void LoadSave(string path)
    {
        try
        {
            var (loadedGame, data) = SaveFileStore.Load(path);
            StartGame(loadedGame, path, data.Name, data.CameraX, data.CameraY);
        }
        catch (Exception ex) { SetMenuMessage($"Nu am putut încărca salvarea: {ex.Message}"); }
    }

    void BeginRename(string path, string currentName)
    {
        pendingRenamePath = path;
        GtkNative.gtk_editable_set_text(saveNameEntry, currentName);
        SetMenuMessage("Editează numele în câmpul de sus, apoi apasă «Redenumește salvarea selectată».");
    }

    void RenameSelectedSave()
    {
        var name = ReadSaveName();
        if (pendingRenamePath is null) { SetMenuMessage("Alege «Redenumește» lângă o salvare."); return; }
        if (name.Length == 0) { SetMenuMessage("Numele salvării nu poate fi gol."); return; }
        try
        {
            SaveFileStore.Rename(pendingRenamePath, name);
            if (string.Equals(activeSavePath, pendingRenamePath, StringComparison.Ordinal)) activeSaveName = name;
            pendingRenamePath = null;
            RebuildMainMenu($"Salvarea a fost redenumită în «{name}».");
        }
        catch (Exception ex) { SetMenuMessage($"Nu am putut redenumi salvarea: {ex.Message}"); }
    }

    void DeleteSave(string path)
    {
        try
        {
            SaveFileStore.Delete(path);
            if (string.Equals(activeSavePath, path, StringComparison.Ordinal))
            { activeSavePath = null; activeSaveName = null; }
            RebuildMainMenu("Salvarea a fost ștearsă.");
        }
        catch (Exception ex) { SetMenuMessage($"Nu am putut șterge salvarea: {ex.Message}"); }
    }

    void StartGame(FactoryGame newGame, string savePath, string saveName, double? savedCameraX = null, double? savedCameraY = null)
    {
        var oldSaveError = SaveCurrent();
        if (oldSaveError is not null) SetMenuMessage(oldSaveError);
        ReleaseCurrentView();
        game = newGame; activeSavePath = savePath; activeSaveName = saveName; pendingRenamePath = null; mode = null;
        cameraWorldX = savedCameraX ?? 0; cameraWorldY = savedCameraY ?? 0;
        if (savedCameraX is { } x && savedCameraY is { } y)
        {
            minChunkX = (int)Math.Floor(x / FactoryGame.Width); minChunkY = (int)Math.Floor(y / FactoryGame.Height);
            maxChunkX = minChunkX + 2; maxChunkY = minChunkY + 2;
        }
        else { minChunkX = -1; minChunkY = -1; maxChunkX = 1; maxChunkY = 1; }
        GtkNative.gtk_window_set_child(window, BuildLayout());
        Refresh(); StartAuto();
    }

    void ShowMainMenu()
    {
        var message = SaveCurrent() ?? "Jocul a fost salvat automat.";
        ReleaseCurrentView();
        GtkNative.gtk_window_set_child(window, BuildMainMenu(message));
    }

    void RebuildMainMenu(string message)
    {
        ReleaseCurrentView();
        GtkNative.gtk_window_set_child(window, BuildMainMenu(message));
    }

    void ReleaseCurrentView()
    {
        StopAuto();
        foreach (var actionWidget in viewActions) Actions.Remove(actionWidget);
        viewActions.Clear(); buildButtons.Clear(); cells.Clear(); cellImages.Clear();
        responsiveSizes.Clear(); responsiveGaps.Clear(); responsiveMargins.Clear();
        pressedMovementKeys.Clear(); scrollRestorePending = false; restoringAdjustments = false;
        cellOverlays.Clear(); beltItemImages.Clear(); beltItemKinds.Clear(); pipeFluidImages.Clear(); pipeFluidKinds.Clear();
        cellSpriteFrames.Clear(); cellSpriteFrameIndices.Clear();
        if (spriteTimerId != 0) { GtkNative.g_source_remove(spriteTimerId); spriteTimerId = 0; }
        if (resizeTimerId != 0) { GtkNative.g_source_remove(resizeTimerId); resizeTimerId = 0; }
        if (scrollRestoreTimerId != 0) { GtkNative.g_source_remove(scrollRestoreTimerId); scrollRestoreTimerId = 0; }
        mapGrid = mapScroll = horizontalAdjustment = verticalAdjustment = IntPtr.Zero;
        statusLabel = saveNameEntry = menuStatusLabel = saveListBox = IntPtr.Zero;
        directionHeading = IntPtr.Zero;
        musicToggleButton = IntPtr.Zero;
    }

    string ReadSaveName() => Marshal.PtrToStringUTF8(GtkNative.gtk_editable_get_text(saveNameEntry))?.Trim() ?? "";

    void SetMenuMessage(string message)
    { if (menuStatusLabel != IntPtr.Zero) GtkNative.gtk_label_set_text(menuStatusLabel, message); }

    string? SaveCurrent()
    {
        if (activeSavePath is null || activeSaveName is null) return null;
        try
        {
            if (mapScroll != IntPtr.Zero && horizontalAdjustment != IntPtr.Zero && verticalAdjustment != IntPtr.Zero)
            {
                if (centerPending || resizeTimerId != 0 || scrollRestoreTimerId != 0)
                {
                    cameraWorldX = minChunkX * FactoryGame.Width + pendingScrollX / tileSize;
                    cameraWorldY = minChunkY * FactoryGame.Height + pendingScrollY / tileSize;
                }
                else
                {
                    cameraWorldX = minChunkX * FactoryGame.Width + GtkNative.gtk_adjustment_get_value(horizontalAdjustment) / tileSize;
                    cameraWorldY = minChunkY * FactoryGame.Height + GtkNative.gtk_adjustment_get_value(verticalAdjustment) / tileSize;
                }
            }
            SaveFileStore.Save(activeSavePath, activeSaveName, game, cameraWorldX, cameraWorldY);
            return null;
        }
        catch (Exception ex) { return $"Salvarea a eșuat: {ex.Message}"; }
    }

    void SaveFromGame()
    {
        var error = SaveCurrent();
        SetMessage(error ?? $"Salvarea «{activeSaveName}» a fost scrisă în {SaveFileStore.SaveDirectory}.");
    }

    void StartBackgroundMusic()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "asstets", "music");
        if (!Directory.Exists(directory)) return;
        musicTracks = Directory.GetFiles(directory, "*.wav").Order(StringComparer.Ordinal).ToArray();
        if (musicTracks.Length == 0) return;
        musicTrackIndex = Random.Shared.Next(musicTracks.Length);
        musicStream = GtkNative.gtk_media_file_new_for_filename(musicTracks[musicTrackIndex]);
        if (musicStream == IntPtr.Zero) return;
        GtkNative.g_signal_connect_data(musicStream, "notify::ended", Marshal.GetFunctionPointerForDelegate(MusicEndedCallback), IntPtr.Zero, IntPtr.Zero, 0);
        GtkNative.gtk_media_stream_set_loop(musicStream, musicTracks.Length == 1);
        GtkNative.gtk_media_stream_set_volume(musicStream, 0.72);
        if (musicEnabled) GtkNative.gtk_media_stream_play(musicStream);
    }

    void PlayNextMusicTrack()
    {
        if (musicStream == IntPtr.Zero || musicTracks.Length == 0 || !musicEnabled) return;
        if (musicTracks.Length == 1)
        {
            GtkNative.gtk_media_file_set_filename(musicStream, musicTracks[0]);
            GtkNative.gtk_media_stream_play(musicStream);
            return;
        }
        var next = Random.Shared.Next(musicTracks.Length - 1);
        if (next >= musicTrackIndex) next++;
        musicTrackIndex = next;
        GtkNative.gtk_media_file_set_filename(musicStream, musicTracks[musicTrackIndex]);
        GtkNative.gtk_media_stream_play(musicStream);
    }

    static void OnMusicEnded(IntPtr stream, IntPtr property, IntPtr data)
    {
        if (current is not null && stream == current.musicStream && current.musicEnabled
            && GtkNative.gtk_media_stream_get_ended(stream))
            current.PlayNextMusicTrack();
    }

    void ToggleMusic()
    {
        musicEnabled = !musicEnabled;
        if (musicStream != IntPtr.Zero)
        {
            if (musicEnabled)
            {
                if (GtkNative.gtk_media_stream_get_ended(musicStream)) PlayNextMusicTrack();
                else GtkNative.gtk_media_stream_play(musicStream);
            }
            else GtkNative.gtk_media_stream_pause(musicStream);
        }
        UpdateMusicButton();
    }

    void UpdateMusicButton()
    {
        if (musicToggleButton != IntPtr.Zero)
            GtkNative.gtk_button_set_label(musicToggleButton, musicEnabled ? "♫  Muzică pornită" : "♫  Muzică oprită");
    }

    static void InstallTheme()
    {
        const string css = """
            window { background: #111715; color: #eff3ea; font-family: "DejaVu Sans", sans-serif; }
            label { color: #e9eee5; }
            .muted { color: #8e9a90; font-size: 78%; }
            .book-entry { color: #ccd5ca; font-size: 78%; padding: 1%; }
            .title { font-size: 130%; font-weight: bold; }
            .brand { color: #b8e068; font-size: 140%; font-weight: bold; }
            .card { background: #1a231f; border: none; border-radius: 2%; padding: 1%; }
            .metric { background: #1c2621; border: none; border-radius: 2%; padding: 1%; }
            .value { color: #eef4e7; font-family: monospace; font-size: 115%; font-weight: bold; }
            .accent { color: #b8e068; }
            button { color: #e4ebdf; background: #202b24; border: none; border-radius: 1%; padding: 1%; }
            button:hover { background: #30402f; border-color: #89a65a; }
            button.suggested-action { background: #3a502d; color: #eef6df; border-color: #799a4b; font-weight: bold; }
            button.tool { padding: 1.5%; }
            button.building-choice { min-height: 0; padding: 5%; font-size: 75%; }
            button.selected { background: #35462b; border-color: #b8e068; color: #dff2b5; }
            button.danger { color: #efb1a4; }
            button.danger.selected { background: #552e2b; border-color: #db7766; color: #ffe2dc; }
            button.tile { min-width: 0; min-height: 0; padding: 0; font-size: 85%; font-weight: bold; border-radius: 0; }
            button.tile.empty { background: #2b3c2c; border-color: #344735; color: #9cae8e; }
            button.tile.iron { background: #404a43; border-color: #68756c; color: #e0e6dc; }
            button.tile.copper { background: #583e2b; border-color: #a8754c; color: #f0bb87; }
            button.tile.tin { background: #495155; border-color: #a4b5b8; color: #e1eff0; }
            button.tile.tree { background: #304a35; border-color: #78a568; color: #c5e9a8; }
            button.tile.oil { background: #40323b; border-color: #a66e6d; color: #f2bb94; }
            button.tile.rubber-tree { background: #304a35; border-color: #78a568; color: #c5e9a8; }
            button.tile.latex-collector { background: #45533b; border-color: #a5bc70; color: #d9e9ac; }
            button.tile.rock { background: #303833; border-color: #48534b; color: #839087; }
            button.tile.miner { background: #35482b; border-color: #8bb35b; color: #d2ee9b; }
            button.tile.belt { background: #37423a; color: #e5ece0; font-size: 105%; }
            button.tile.pipe { background: #293d45; color: #b9e5e8; font-size: 105%; }
            button.tile.pump { background: #55432c; color: #f4d28e; }
            button.tile.fluid-machine { background: #29434a; color: #b0e8de; }
            button.tile.smelter { background: #513e2d; border-color: #bd8953; color: #f2bb7c; }
            button.tile.alloy-smelter { background: #584b33; border-color: #c5a65b; color: #f4d88b; }
            button.tile.rotor-press { background: #344552; border-color: #83a5b4; color: #c3e1ef; }
            button.tile.alloy-smelter { background: #584b33; border-color: #c5a65b; color: #f4d88b; }
            button.tile.rotor-press { background: #344552; border-color: #83a5b4; color: #c3e1ef; }
            button.tile.assembler { background: #304252; border-color: #6c9db3; color: #b4e1f2; }
            button.tile.ship { background: #455338; border-color: #b1ce70; color: #e2f7a9; }
            button.tile.busy { color: #d8b76b; }
            picture.belt-item-texture { background: #17201c; border: none; border-radius: 8%; padding: .3%; }
            scrolledwindow.world-map scrollbar { opacity: 0; }
            progressbar trough { background: #39463c; border-radius: 8%; }
            progressbar progress { background: #b8e068; border-radius: 8%; }
            """;
        var provider = GtkNative.gtk_css_provider_new();
        GtkNative.gtk_css_provider_load_from_data(provider, css, -1);
        var display = GtkNative.gdk_display_get_default();
        GtkNative.gtk_style_context_add_provider_for_display(display, provider, CssPriority);
    }

    IntPtr BuildLayout()
    {
        var root = Box(1, 1.4); Margins(root, 1.6);
        Append(root, BuildHeader()); Append(root, BuildMetrics());
        var body = Box(0, 1.8); GtkNative.gtk_widget_set_vexpand(body, true); Append(root, body);
        Append(body, BuildMapArea()); Append(body, BuildSidebar());
        var footer = Label("FACTORY FRONTIER     ·     UN MIC COLȚ DE LUME, O MARE LINIE DE PRODUCȚIE", "muted");
        Append(root, footer); return root;
    }

    IntPtr BuildHeader()
    {
        var row = Box(0, 1.4); var brand = Label("⚙  FACTORY FRONTIER", "brand"); Append(row, brand);
        var spacer = Label("CONSTRUIEȘTE  ·  AUTOMATIZEAZĂ  ·  LIVREAZĂ", "muted"); GtkNative.gtk_widget_set_hexpand(spacer, true); GtkNative.gtk_label_set_xalign(spacer, 1); Append(row, spacer);
        var save = Button("▣  Salvează"); Connect(save, SaveFromGame); Append(row, save);
        musicToggleButton = Button(""); Connect(musicToggleButton, ToggleMusic); UpdateMusicButton(); Append(row, musicToggleButton);
        var menu = Button("⌂  Meniu principal"); Connect(menu, () => ShowMainMenu()); Append(row, menu);
        return row;
    }

    IntPtr BuildMetrics()
    {
        var row = Box(0, 1);
        Append(row, Metric("BUGET", out moneyLabel)); Append(row, Metric("NIVEL", out levelLabel)); Append(row, Metric("TURĂ", out tickLabel));
        var contract = Box(1, 0.6); GtkNative.gtk_widget_add_css_class(contract, "metric"); GtkNative.gtk_widget_set_hexpand(contract, true);
        progressHeadingLabel = Label("", "muted"); GtkNative.gtk_label_set_wrap(progressHeadingLabel, true); GtkNative.gtk_label_set_xalign(progressHeadingLabel, 0); Append(contract, progressHeadingLabel);
        var status = Box(0, 1.2); deliveryLabel = Label("0 / 3", "value"); Append(status, deliveryLabel);
        progressBar = GtkNative.gtk_progress_bar_new(); GtkNative.gtk_widget_set_hexpand(progressBar, true); GtkNative.gtk_progress_bar_set_fraction(progressBar, 0); Append(status, progressBar); Append(contract, status); Append(row, contract);
        var controls = Box(0, 0.8);
        var advance = Button("⏭  +1 tură"); RequestResponsiveSize(advance, 8, 6.5); Connect(advance, Advance); Append(controls, advance);
        autoButton = Button("⏸  Pauză", "suggested-action"); RequestResponsiveSize(autoButton, 12, 6.5); Connect(autoButton, ToggleAuto); Append(controls, autoButton);
        Append(row, controls);
        return row;
    }

    IntPtr Metric(string caption, out IntPtr value)
    {
        var box = Box(1, 0.5); GtkNative.gtk_widget_add_css_class(box, "metric"); RequestResponsiveSize(box, 12, 6.5);
        Append(box, Label(caption, "muted")); value = Label("", "value"); Append(box, value); return box;
    }

    IntPtr BuildMapArea()
    {
        if (tileSize == 0) tileSize = ResponsiveUnit(4);
        var area = Box(1, 1); GtkNative.gtk_widget_add_css_class(area, "card"); GtkNative.gtk_widget_set_hexpand(area, true); GtkNative.gtk_widget_set_vexpand(area, true);
        var heading = Box(0, 1.2); var titles = Box(1, 0.4); Append(titles, Label("ZONA DE PRODUCȚIE  ·  SECTOR 01", "accent muted"));
        worldNameLabel = Label(activeSaveName ?? "Lume nouă", "title"); Append(titles, worldNameLabel); Append(heading, titles);
        seedLabel = Label("", "muted"); GtkNative.gtk_widget_set_hexpand(seedLabel, true); GtkNative.gtk_label_set_xalign(seedLabel, 1); Append(heading, seedLabel); Append(area, heading);
        mapScroll = GtkNative.gtk_scrolled_window_new(); GtkNative.gtk_widget_add_css_class(mapScroll, "world-map");
        GtkNative.gtk_scrolled_window_set_policy(mapScroll, 1, 1);
        GtkNative.g_signal_connect_data(mapScroll, "notify::width", Marshal.GetFunctionPointerForDelegate(ViewportNotifyCallback), IntPtr.Zero, IntPtr.Zero, 0);
        GtkNative.g_signal_connect_data(mapScroll, "notify::height", Marshal.GetFunctionPointerForDelegate(ViewportNotifyCallback), IntPtr.Zero, IntPtr.Zero, 0);
        GtkNative.gtk_widget_set_hexpand(mapScroll, true); GtkNative.gtk_widget_set_vexpand(mapScroll, true);
        mapGrid = NewMapGrid();
        for (var cy = minChunkY; cy <= maxChunkY; cy++) for (var cx = minChunkX; cx <= maxChunkX; cx++) AttachChunk(cx, cy);
        GtkNative.gtk_scrolled_window_set_child(mapScroll, mapGrid);
        UpdateScrollAdjustments();
        Append(area, mapScroll);
        Append(area, Label("WASD · deplasează harta       ● Fe  Fier       ● Cu  Cupru       ◆ Bolovani       ◇ Clădiri       → Benzi", "muted"));
        ScheduleOriginCenter();
        return area;
    }

    IntPtr NewMapGrid()
    {
        var grid = GtkNative.gtk_grid_new(); GtkNative.gtk_grid_set_row_spacing(grid, 0); GtkNative.gtk_grid_set_column_spacing(grid, 0);
        GtkNative.gtk_grid_set_column_homogeneous(grid, true); GtkNative.gtk_grid_set_row_homogeneous(grid, true);
        GtkNative.gtk_widget_set_halign(grid, 0); GtkNative.gtk_widget_set_valign(grid, 0);
        GtkNative.gtk_widget_set_hexpand(grid, true); GtkNative.gtk_widget_set_vexpand(grid, true);
        return grid;
    }

    void AttachChunk(int chunkX, int chunkY)
    {
        game.EnsureChunkGenerated(chunkX, chunkY);
        for (var localY = 0; localY < FactoryGame.Height; localY++) for (var localX = 0; localX < FactoryGame.Width; localX++)
        {
            var x = chunkX * FactoryGame.Width + localX; var y = chunkY * FactoryGame.Height + localY;
            var button = Button(""); GtkNative.gtk_widget_add_css_class(button, "tile");
            GtkNative.gtk_widget_set_size_request(button, tileSize, tileSize);
            Connect(button, () => OnCell(x, y)); cells[(x, y)] = button;
            GtkNative.gtk_grid_attach(mapGrid, button, x - minChunkX * FactoryGame.Width, y - minChunkY * FactoryGame.Height, 1, 1);
            RenderCell(x, y, button);
        }
    }

    void ResetWorldView()
    {
        foreach (var button in cells.Values) Actions.Remove(button);
        cellImages.Clear();
        cellOverlays.Clear(); beltItemImages.Clear(); beltItemKinds.Clear();
        pipeFluidImages.Clear(); pipeFluidKinds.Clear();
        cellSpriteFrames.Clear(); cellSpriteFrameIndices.Clear();
        if (spriteTimerId != 0) { GtkNative.g_source_remove(spriteTimerId); spriteTimerId = 0; }
        minChunkX = -1; minChunkY = -1; maxChunkX = 1; maxChunkY = 1; cells.Clear();
        mapGrid = NewMapGrid();
        for (var cy = minChunkY; cy <= maxChunkY; cy++) for (var cx = minChunkX; cx <= maxChunkX; cx++) AttachChunk(cx, cy);
        GtkNative.gtk_scrolled_window_set_child(mapScroll, mapGrid);
        UpdateScrollAdjustments();
        ScheduleOriginCenter();
    }

    void ScheduleOriginCenter()
    {
        centerPending = true;
        pendingScrollX = (cameraWorldX - minChunkX * FactoryGame.Width) * tileSize;
        pendingScrollY = (cameraWorldY - minChunkY * FactoryGame.Height) * tileSize;
        if (scrollRestoreTimerId != 0) GtkNative.g_source_remove(scrollRestoreTimerId);
        scrollRestoreTimerId = GtkNative.g_timeout_add(100, Marshal.GetFunctionPointerForDelegate(ScrollRestoreCallback), IntPtr.Zero);
    }

    void UpdateScrollAdjustments()
    {
        var nextH = GtkNative.gtk_scrolled_window_get_hadjustment(mapScroll);
        var nextV = GtkNative.gtk_scrolled_window_get_vadjustment(mapScroll);
        if (horizontalAdjustment != nextH)
        {
            horizontalAdjustment = nextH;
            GtkNative.g_signal_connect_data(horizontalAdjustment, "value-changed", Marshal.GetFunctionPointerForDelegate(AdjustmentCallback), IntPtr.Zero, IntPtr.Zero, 0);
        }
        if (verticalAdjustment != nextV)
        {
            verticalAdjustment = nextV;
            GtkNative.g_signal_connect_data(verticalAdjustment, "value-changed", Marshal.GetFunctionPointerForDelegate(AdjustmentCallback), IntPtr.Zero, IntPtr.Zero, 0);
        }
    }

    void OnScrolled()
    {
        if (restoringAdjustments || scrollRestorePending) return;
        if (centerPending) { RefreshVisibleCells(); return; }
        if (extendingWorld) return;
        var h = GtkNative.gtk_adjustment_get_value(horizontalAdjustment);
        var v = GtkNative.gtk_adjustment_get_value(verticalAdjustment);
        var hMax = GtkNative.gtk_adjustment_get_upper(horizontalAdjustment) - GtkNative.gtk_adjustment_get_page_size(horizontalAdjustment);
        var vMax = GtkNative.gtk_adjustment_get_upper(verticalAdjustment) - GtkNative.gtk_adjustment_get_page_size(verticalAdjustment);
        var shiftX = 0d; var shiftY = 0d;
        extendingWorld = true;
        const int maxRenderedChunksPerAxis = 3;
        var chunkPixelWidth = FactoryGame.Width * (double)tileSize;
        var chunkPixelHeight = FactoryGame.Height * (double)tileSize;
        if (h < 70)
        {
            minChunkX--;
            if (maxChunkX - minChunkX + 1 > maxRenderedChunksPerAxis)
            { RemoveChunkColumn(maxChunkX); maxChunkX--; }
            ReattachWorld();
            for (var cy = minChunkY; cy <= maxChunkY; cy++) AttachChunk(minChunkX, cy);
            shiftX = chunkPixelWidth;
        }
        else if (hMax - h < 70)
        {
            maxChunkX++;
            if (maxChunkX - minChunkX + 1 > maxRenderedChunksPerAxis)
            { RemoveChunkColumn(minChunkX); minChunkX++; ReattachWorld(); shiftX = -chunkPixelWidth; }
            for (var cy = minChunkY; cy <= maxChunkY; cy++) AttachChunk(maxChunkX, cy);
        }
        if (v < 70)
        {
            minChunkY--;
            if (maxChunkY - minChunkY + 1 > maxRenderedChunksPerAxis)
            { RemoveChunkRow(maxChunkY); maxChunkY--; }
            ReattachWorld();
            for (var cx = minChunkX; cx <= maxChunkX; cx++) AttachChunk(cx, minChunkY);
            shiftY = chunkPixelHeight;
        }
        else if (vMax - v < 70)
        {
            maxChunkY++;
            if (maxChunkY - minChunkY + 1 > maxRenderedChunksPerAxis)
            { RemoveChunkRow(minChunkY); minChunkY++; ReattachWorld(); shiftY = -chunkPixelHeight; }
            for (var cx = minChunkX; cx <= maxChunkX; cx++) AttachChunk(cx, maxChunkY);
        }
        extendingWorld = false;
        if (shiftX > 0 || shiftY > 0)
        {
            pendingScrollX = h + shiftX; pendingScrollY = v + shiftY;
            scrollRestorePending = true;
            if (scrollRestoreTimerId != 0) GtkNative.g_source_remove(scrollRestoreTimerId);
            scrollRestoreTimerId = GtkNative.g_timeout_add(50, Marshal.GetFunctionPointerForDelegate(ScrollRestoreCallback), IntPtr.Zero);
        }
        // The visible region is small (at most 3×3 chunks); refresh it so item
        // textures on belts remain in sync after returning to an unloaded view.
        RefreshVisibleCells();
    }

    void ReattachWorld()
    {
        var wasExtending = extendingWorld;
        extendingWorld = true;
        foreach (var button in cells.Values) GtkNative.g_object_ref(button);
        foreach (var button in cells.Values.ToArray()) GtkNative.gtk_grid_remove(mapGrid, button);
        foreach (var (point, button) in cells)
        {
            GtkNative.gtk_grid_attach(mapGrid, button, point.x - minChunkX * FactoryGame.Width, point.y - minChunkY * FactoryGame.Height, 1, 1);
            GtkNative.g_object_unref(button);
        }
        extendingWorld = wasExtending;
    }

    static void OnViewportNotify(IntPtr widget, IntPtr property, IntPtr data) => current?.ResizeTilesToViewport();
    static void OnWindowSizeNotify(IntPtr widget, IntPtr property, IntPtr data) => current?.ApplyResponsiveLayout();

    void RequestResponsiveSize(IntPtr widget, double widthPercent, double heightPercent = 0)
    {
        responsiveSizes[widget] = (widthPercent, heightPercent);
        ApplyResponsiveSize(widget, widthPercent, heightPercent);
    }

    void ApplyResponsiveSize(IntPtr widget, double widthPercent, double heightPercent)
    {
        var width = widthPercent > 0 ? PercentOf(GtkNative.gtk_widget_get_width(window), widthPercent) : -1;
        var height = heightPercent > 0 ? PercentOf(GtkNative.gtk_widget_get_height(window), heightPercent) : -1;
        GtkNative.gtk_widget_set_size_request(widget, width, height);
    }

    static int PercentOf(int extent, double percent) => (int)Math.Round(extent * percent / 100);

    int ResponsiveUnit(double percent)
    {
        var width = GtkNative.gtk_widget_get_width(window);
        var height = GtkNative.gtk_widget_get_height(window);
        return PercentOf(Math.Min(width, height), percent);
    }

    void ApplyResponsiveLayout()
    {
        if (window == IntPtr.Zero) return;
        foreach (var (widget, size) in responsiveSizes.ToArray())
            ApplyResponsiveSize(widget, size.width, size.height);
        foreach (var (box, percent) in responsiveGaps.ToArray()) GtkNative.gtk_box_set_spacing(box, ResponsiveUnit(percent));
        foreach (var (widget, percent) in responsiveMargins.ToArray())
        {
            var margin = ResponsiveUnit(percent);
            GtkNative.gtk_widget_set_margin_start(widget, margin); GtkNative.gtk_widget_set_margin_end(widget, margin);
            GtkNative.gtk_widget_set_margin_top(widget, margin); GtkNative.gtk_widget_set_margin_bottom(widget, margin);
        }
        if (mapScroll != IntPtr.Zero) ResizeTilesToViewport();
    }

    void ResizeTilesToViewport()
    {
        var viewportWidth = GtkNative.gtk_widget_get_width(mapScroll);
        var viewportHeight = GtkNative.gtk_widget_get_height(mapScroll);
        if (viewportWidth <= 0 || viewportHeight <= 0 || horizontalAdjustment == IntPtr.Zero || verticalAdjustment == IntPtr.Zero) return;

        // Size tiles to show roughly the same number of columns and rows in
        // both a maximized and a smaller window, while keeping them square.
        var newSize = (int)Math.Round(Math.Min(viewportWidth * .0556, viewportHeight * .0833));
        if (newSize <= 0) return;
        if (newSize == tileSize) return;

        var oldSize = tileSize;
        var preserveCenteredOrigin = centerPending;
        var horizontalTile = preserveCenteredOrigin
            ? -minChunkX * FactoryGame.Width
            : oldSize > 0 ? GtkNative.gtk_adjustment_get_value(horizontalAdjustment) / oldSize : 0;
        var verticalTile = preserveCenteredOrigin
            ? -minChunkY * FactoryGame.Height
            : oldSize > 0 ? GtkNative.gtk_adjustment_get_value(verticalAdjustment) / oldSize : 0;

        tileSize = newSize;
        foreach (var (point, button) in cells)
        {
            GtkNative.gtk_widget_set_size_request(button, tileSize, tileSize);
            if (cellImages.TryGetValue(point, out var image))
                GtkNative.gtk_widget_set_size_request(image, (int)Math.Round(tileSize * .9), (int)Math.Round(tileSize * .9));
            if (beltItemImages.TryGetValue(point, out var itemImage))
            {
                var iconSize = BeltItemIconSize(beltItemKinds.GetValueOrDefault(point));
                GtkNative.gtk_widget_set_size_request(itemImage, iconSize, iconSize);
                ApplyBeltItemMotion(point, itemImage);
            }
            if (pipeFluidImages.ContainsKey(point))
            {
                var fluidIconSize = (int)Math.Round(tileSize * .64);
                GtkNative.gtk_widget_set_size_request(pipeFluidImages[point], fluidIconSize, fluidIconSize);
            }
        }

        pendingScrollX = horizontalTile * tileSize;
        pendingScrollY = verticalTile * tileSize;
        centerPending = true;
        if (resizeTimerId != 0) GtkNative.g_source_remove(resizeTimerId);
        resizeTimerId = GtkNative.g_timeout_add(80, Marshal.GetFunctionPointerForDelegate(ResizeRestoreCallback), IntPtr.Zero);
    }

    void RemoveChunkColumn(int chunkX)
    {
        for (var chunkY = minChunkY; chunkY <= maxChunkY; chunkY++) RemoveChunk(chunkX, chunkY);
    }

    void RemoveChunkRow(int chunkY)
    {
        for (var chunkX = minChunkX; chunkX <= maxChunkX; chunkX++) RemoveChunk(chunkX, chunkY);
    }

    void RemoveChunk(int chunkX, int chunkY)
    {
        for (var localY = 0; localY < FactoryGame.Height; localY++)
        for (var localX = 0; localX < FactoryGame.Width; localX++)
        {
            var point = (chunkX * FactoryGame.Width + localX, chunkY * FactoryGame.Height + localY);
            if (!cells.Remove(point, out var button)) continue;
            Actions.Remove(button);
            cellImages.Remove(point);
            cellOverlays.Remove(point); beltItemImages.Remove(point); beltItemKinds.Remove(point);
            pipeFluidImages.Remove(point); pipeFluidKinds.Remove(point);
            cellSpriteFrames.Remove(point); cellSpriteFrameIndices.Remove(point);
            GtkNative.gtk_grid_remove(mapGrid, button);
        }
        StopSpriteTimerIfIdle();
    }

    IntPtr BuildSidebar()
    {
        var side = Box(1, 1.2); RequestResponsiveSize(side, 18);
        var buildingCard = Box(1, 0.7); GtkNative.gtk_widget_add_css_class(buildingCard, "card");
        Append(buildingCard, Label("CLĂDIRI", "accent muted"));
        Append(buildingCard, Label("Alege și plasează", "title"));
        var buildingGrid = GtkNative.gtk_grid_new();
        GtkNative.gtk_grid_set_column_spacing(buildingGrid, 3);
        GtkNative.gtk_grid_set_row_spacing(buildingGrid, 3);
        GtkNative.gtk_widget_set_hexpand(buildingGrid, true);
        Append(buildingCard, buildingGrid);
        var choices = new List<(string label, string key, Action select)>();
        choices.Add(("⛏ Miner · $25", "miner", () => SelectBuildMode("miner")));
        choices.Add(("➜ Bandă · $2", "belt", () => SelectBuildMode("belt")));
        choices.Add(("◉ Țeavă · $2", "pipe", () => SelectBuildMode("pipe")));
        choices.Add(("⛽ Pompă · $90", "pump", () => SelectBuildMode("pump")));
        choices.Add(("🌳 Colector latex · $80", "latex_collector", () => SelectBuildMode("latex_collector")));
        choices.Add(("🚀 Navă cargo · $120", "ship", () => SelectBuildMode("ship")));
        choices.Add(("⌫ Demolează", "delete", () => SelectBuildMode("delete")));
        foreach (var recipe in RecipeBook.All)
        {
            var captured = recipe;
            var machineCost = RecipeBook.BuildCost(recipe);
            choices.Add(($"▦ {MachineName(recipe.Id)} · ${machineCost}", $"machine:{recipe.Id}", () =>
            {
                selectedRecipeId = captured.Id; mode = "machine"; RefreshToolSelection();
                SetMessage($"Plasează {MachineName(captured.Id)} ({captured.Name}) pe un loc liber.");
            }));
        }
        foreach (var recipe in FluidRecipeBook.All)
        {
            var captured = recipe;
            var machineCost = recipe.Machine == TileKind.Distillery ? 70 : recipe.Machine == TileKind.ChemicalReactor ? 90 : 100;
            choices.Add(($"⚗ {recipe.MachineName} · ${machineCost}", $"fluid_machine:{recipe.Id}", () =>
            {
                selectedFluidRecipeId = captured.Id; mode = "fluid_machine"; RefreshToolSelection();
                SetMessage($"Plasează {captured.MachineName} pentru rețeta {captured.Name}.");
            }));
        }
        for (var i = 0; i < choices.Count; i++)
        {
            var choice = choices[i]; var button = Button(choice.label, "tool building-choice");
            GtkNative.gtk_widget_set_hexpand(button, true);
            var buttonLabel = GtkNative.gtk_widget_get_first_child(button);
            if (buttonLabel != IntPtr.Zero) { GtkNative.gtk_label_set_wrap(buttonLabel, true); GtkNative.gtk_label_set_xalign(buttonLabel, .5f); }
            buildButtons[choice.key] = button;
            Connect(button, choice.select);
            GtkNative.gtk_grid_attach(buildingGrid, button, i % 2, i / 2, 1, 1);
        }
        Append(side, buildingCard);

        optionsMiner = Box(1, 0.5); GtkNative.gtk_widget_set_visible(optionsMiner, false);
        Append(optionsMiner, Label("TIPUL MINEREULUI", "muted")); var ores = Box(0, 0.6);
        oreIronButton = Button("Fe · Fier"); oreCopperButton = Button("Cu · Cupru"); oreTinButton = Button("Sn · Staniu");
        Connect(oreIronButton, () => { ore = ItemKind.IronOre; SetOptionSelection(); }); Connect(oreCopperButton, () => { ore = ItemKind.CopperOre; SetOptionSelection(); });
        Connect(oreTinButton, () => { ore = ItemKind.TinOre; SetOptionSelection(); });
        Append(ores, oreIronButton); Append(ores, oreCopperButton); Append(ores, oreTinButton); Append(optionsMiner, ores); Append(side, optionsMiner);
        optionsBelt = Box(1, 0.5); GtkNative.gtk_widget_set_visible(optionsBelt, false);
        directionHeading = Label("DIRECȚIA FLUXULUI", "muted"); Append(optionsBelt, directionHeading);
        var dirs = Box(0, 0.6);
        foreach (var (glyph, dir) in new[] { ("↑", Direction.North), ("→", Direction.East), ("↓", Direction.South), ("←", Direction.West) })
        { var b = Button(glyph); Connect(b, () => { direction = dir; UpdateDirectionButtons(dirs); }); Append(dirs, b); }
        Append(optionsBelt, dirs); Append(side, optionsBelt);
        Append(side, BuildEncyclopedia());
        var fluidHelp = Label("Țevile transportă fluide spre utilaje și nave. Colectorul trebuie pus lângă un arbore de cauciuc; pompa lângă petrol.", "muted");
        GtkNative.gtk_label_set_wrap(fluidHelp, true); Append(side, fluidHelp);

        var mission = Box(1, 0.8); GtkNative.gtk_widget_add_css_class(mission, "card");
        Append(mission, Label("✦  PROGRESUL FABRICII", "accent muted")); Append(mission, Label("12 niveluri · 16 rețete", "title"));
        var missionText = Label("Livrează obiectivele afișate pentru nivel: produse solide pe benzi și fluide prin țevi către nave. Toate livrările aduc venit. Polietilena și lipiciul intră în rețeta cadrului. Nave cargo noi: $120.", "muted"); GtkNative.gtk_label_set_wrap(missionText, true); Append(mission, missionText);
        Append(side, mission);
        statusLabel = Label("Plasează un miner lângă un zăcământ ca să începi.", "muted"); GtkNative.gtk_label_set_wrap(statusLabel, true);
        var status = Box(0, 0.9); GtkNative.gtk_widget_add_css_class(status, "card"); Append(status, Label("⌁", "accent")); Append(status, statusLabel); Append(side, status);
        var scroller = GtkNative.gtk_scrolled_window_new();
        GtkNative.gtk_scrolled_window_set_policy(scroller, 2, 1);
        GtkNative.gtk_scrolled_window_set_child(scroller, side);
        RequestResponsiveSize(scroller, 18);
        GtkNative.gtk_widget_set_vexpand(scroller, true);
        return scroller;
    }

    IntPtr BuildEncyclopedia()
    {
        var card = Box(1, 0.45); GtkNative.gtk_widget_add_css_class(card, "card");
        Append(card, Label("CARTEA FABRICII", "accent muted"));
        Append(card, Label("Iteme, fluide și rețete", "title"));
        Append(card, Label("ITEME", "accent muted"));
        foreach (var item in Enum.GetValues<ItemKind>())
        {
            var producedBy = RecipeBook.All.FirstOrDefault(recipe => recipe.Output == item);
            var usedBy = RecipeBook.All.Where(recipe => recipe.Inputs.ContainsKey(item)).Select(recipe => recipe.Name).ToArray();
            var detail = producedBy is not null ? $"produs la: {MachineName(producedBy.Id)}" : "extras de miner";
            if (usedBy.Length > 0) detail += $" · folosit la: {string.Join(", ", usedBy)}";
            AppendBookEntry(card, $"{ItemName(item)} — {detail}");
        }

        Append(card, Label("FLUIDE", "accent muted"));
        foreach (var fluid in Enum.GetValues<FluidKind>())
        {
            var producer = FluidRecipeBook.All.FirstOrDefault(recipe => recipe.Output == fluid);
            var smelterProducer = RecipeBook.All.FirstOrDefault(recipe => recipe.FluidProduct == fluid);
            var consumers = RecipeBook.All.Where(recipe => recipe.FluidInputs.ContainsKey(fluid)).Select(recipe => recipe.Name).ToArray();
            var detail = producer is not null ? $"produs în {producer.MachineName} din {FluidName(producer.Input)}"
                : smelterProducer is not null ? $"produs în {MachineName(smelterProducer.Id)} din {string.Join(" + ", smelterProducer.Inputs.Select(input => ItemName(input.Key)))}"
                : fluid == FluidKind.Petroleum ? "extras de pompa de petrol"
                : fluid == FluidKind.Latex ? "extras de colectorul de lângă arborele de cauciuc" : "fluid de producție";
            if (consumers.Length > 0) detail += $" · folosit la: {string.Join(", ", consumers)}";
            AppendBookEntry(card, $"{FluidName(fluid)} — {detail}");
        }

        Append(card, Label("REȚETE", "accent muted"));
        foreach (var recipe in RecipeBook.All)
        {
            var inputs = string.Join(" + ", recipe.Inputs.Select(input => $"{input.Value}× {ItemName(input.Key)}")
                .Concat(recipe.FluidInputs.Select(input => $"{input.Value}× {FluidName(input.Key)}")));
            var output = recipe.FluidProduct is { } fluid ? FluidName(fluid)
                : recipe.Output is { } item ? ItemName(item) : recipe.Product;
            AppendBookEntry(card, $"Niv. {recipe.Level} · {MachineName(recipe.Id)}: {inputs} → {output}");
        }
        foreach (var recipe in FluidRecipeBook.All)
            AppendBookEntry(card, $"{recipe.MachineName}: {FluidName(recipe.Input)} → {FluidName(recipe.Output)}");
        return card;
    }

    void AppendBookEntry(IntPtr parent, string text)
    {
        var entry = Label(text, "book-entry");
        GtkNative.gtk_label_set_wrap(entry, true);
        GtkNative.gtk_label_set_xalign(entry, 0);
        Append(parent, entry);
    }

    void SelectBuildMode(string selectedMode)
    {
        mode = mode == selectedMode ? null : selectedMode;
        RefreshToolSelection();
        SetMessage(mode is null ? "Selectează o clădire sau inspectează harta." : "Selectează un loc liber pe hartă pentru construcție.");
    }

    static string MachineName(string recipeId) => recipeId switch
    {
        "iron_plate" => "Iron Smelter", "copper_plate" => "Copper Smelter", "soldering_alloy" => "Tin Smelter",
        "bronze" => "Alloy Smelter", "rotor" => "Rotor Press", "pump_component" => "Pump Assembler",
        "mainframe" => "Mainframe Fabricator", "thruster" => "Thruster Fabricator",
        "copper_wire" => "Wiremill", "iron_gear" => "Gear Press",
        "circuit" => "Circuit Assembler", "frame" => "Extruder",
        "motor" => "Motor Assembler", "advanced_circuit" => "PCB Fabricator",
        "processor" => "Processor Assembler", "rocket_part" => "Rocket Fabricator",
        _ => "Factory Machine"
    };

    static string MachineSpriteFolder(string recipeId) => recipeId switch
    {
        "iron_plate" => "iron_smelter", "copper_plate" => "copper_smelter", "soldering_alloy" => "tin_smelter",
        "copper_wire" => "wiremill", "iron_gear" => "gear_press",
        "circuit" => "circuit_assembler", "frame" => "extruder",
        "motor" => "motor_assembler", "advanced_circuit" => "pcb_fabricator",
        "processor" => "processor_assembler", "rocket_part" => "rocket_fabricator",
        "bronze" => "alloy_smelter", "rotor" => "rotor_press", "pump_component" => "pump_assembler",
        "mainframe" => "mainframe_fabricator", "thruster" => "thruster_fabricator",
        _ => "assembler"
    };

    static string FluidMachineSpriteFolder(string recipeId) => recipeId switch
    {
        "petroleum_to_ethanol" => "distillery", "ethanol_to_ethylene" => "chemical_reactor",
        "ethylene_to_polyethylene" => "polymerizer", "latex_to_glue" => "glue_mixer", _ => "chemical_reactor"
    };

    static string FluidName(FluidKind fluid) => fluid switch
    {
        FluidKind.Petroleum => "petrol", FluidKind.Ethanol => "etanol",
        FluidKind.Ethylene => "etilenă", FluidKind.Polyethylene => "polietilenă", FluidKind.Latex => "latex",
        FluidKind.Glue => "lipici", _ => "aliaj de lipit"
    };

    static string ItemName(ItemKind item) => item switch
    {
        ItemKind.IronOre => "minereu de fier", ItemKind.CopperOre => "minereu de cupru", ItemKind.TinOre => "minereu de staniu",
        ItemKind.IronPlate => "placă de fier", ItemKind.CopperPlate => "placă de cupru",
        ItemKind.CopperWire => "fir de cupru", ItemKind.Gear => "angrenaj", ItemKind.Circuit => "circuit",
        ItemKind.Frame => "cadru", ItemKind.Motor => "motor", ItemKind.AdvancedCircuit => "circuit avansat",
        ItemKind.Processor => "procesor", ItemKind.RocketPart => "piesă de rachetă",
        ItemKind.Bronze => "bronz", ItemKind.Rotor => "rotor", ItemKind.Pump => "pump",
        ItemKind.Mainframe => "mainframe", ItemKind.Thruster => "thruster", _ => "piesă de rachetă"
    };

    static string FluidMachineStatus(Tile tile)
    {
        if (tile.FluidOutput is { } output) return $"așteaptă o țeavă liberă pentru {FluidName(output)}";
        if (tile.FluidInput is { } input) return $"procesează {FluidName(input)}";
        return "așteaptă fluid la intrare";
    }

    void OnCell(int x, int y)
    {
        if (mode is null)
        {
            var tile = game.At(x, y); var extra = tile.Stored > 0 ? $" · {tile.Stored} unități în așteptare" : "";
            if (tile.InstalledRecipe is { } recipe)
                extra += $" · Rețetă: {recipe.Name} · {MachineStatus(tile, recipe)}";
            if (tile.InstalledFluidRecipe is { } fluidRecipe)
                extra += $" · {fluidRecipe.Name} · {FluidMachineStatus(tile)}";
            if (tile.Kind == TileKind.Pipe)
                extra += tile.PipeFluid is { } fluid ? $" · transportă {FluidName(fluid)}" : " · goală";
            var name = tile.InstalledRecipe is { } installed ? MachineName(installed.Id)
                : tile.InstalledFluidRecipe is { } chemical ? chemical.MachineName : TileName(tile.Kind);
            SetMessage($"{name} · coordonatele {x}, {y}{extra}"); return;
        }
        if (mode == "delete") SetMessage(game.Delete(x, y));
        else SetMessage(game.Build(mode, x, y, ore, direction, selectedRecipeId, selectedFluidRecipeId));
        if (cells.TryGetValue((x, y), out var button)) RenderCell(x, y, button);
        Refresh();
    }

    void Advance()
    {
        var oldLevel = game.Level;
        game.Advance();
        beltMotionProgress = 0;
        RefreshLoadedTransporters();
        Refresh();
        var saleMessage = game.LastIncome > 0 ? $"Vândut: {game.LastSales} · +${game.LastIncome}. " : "";
        SetMessage(saleMessage + (game.Level > oldLevel
            ? $"Nivel {game.Level}! Ai deblocat rețeta «{game.UnlockedRecipes[^1].Name}». Utilajele existente își păstrează rețetele."
            : $"Tura {game.Tick} · progres obiective {game.LevelProgress}/{Math.Max(1, game.CurrentContract.Total)}."));
    }

    static string ContractDescription(DeliveryContract contract) => string.Join(" · ", contract.Items.Select(i => $"{i.Value}× {ItemName(i.Key)}").Concat(contract.Fluids.Select(i => $"{i.Value}× {FluidName(i.Key)}")));

    static string MachineStatus(Tile tile, FactoryRecipe recipe)
    {
        if (tile.Output.Count > 0) return "produsul așteaptă la ieșire";
        if (tile.FluidOutput is { } output) return $"așteaptă o țeavă liberă pentru {FluidName(output)}";
        var missing = recipe.Inputs
            .Where(input => tile.Stock.GetValueOrDefault(input.Key) < input.Value)
            .Select(input => $"{input.Value - tile.Stock.GetValueOrDefault(input.Key)}× {ItemName(input.Key)}")
            .ToArray();
        var missingFluids = recipe.FluidInputs.Where(input => tile.FluidStock.GetValueOrDefault(input.Key) < input.Value).Select(input => $"{input.Value - tile.FluidStock.GetValueOrDefault(input.Key)}× {FluidName(input.Key)}");
        var requirements = missing.Concat(missingFluids).ToArray();
        return requirements.Length > 0 ? $"așteaptă {string.Join(", ", requirements)}" : "gata de producție la următoarea tură";
    }

    void ToggleAuto()
    {
        if (autoRunning) StopAuto(); else StartAuto();
    }

    void StartAuto()
    {
        if (autoRunning) return;
        timerId = GtkNative.g_timeout_add(1000, Marshal.GetFunctionPointerForDelegate(TimeCallback), IntPtr.Zero);
        autoRunning = true;
        GtkNative.gtk_button_set_label(autoButton, "⏸  Pauză");
        GtkNative.gtk_widget_add_css_class(autoButton, "suggested-action");
        EnsureBeltMotionTimer();
    }

    void StopAuto()
    {
        if (!autoRunning) return;
        GtkNative.g_source_remove(timerId); timerId = 0; autoRunning = false;
        if (beltMotionTimerId != 0) { GtkNative.g_source_remove(beltMotionTimerId); beltMotionTimerId = 0; }
        GtkNative.gtk_button_set_label(autoButton, "▶  Pornește timpul");
        GtkNative.gtk_widget_remove_css_class(autoButton, "suggested-action");
    }

    static int OnTimer(IntPtr data)
    {
        current?.Advance();
        return 1;
    }

    static int OnScrollRestore(IntPtr data)
    {
        if (current is null) return 0;
        current.scrollRestoreTimerId = 0;
        if (current.mapScroll == IntPtr.Zero) return 0;
        current.restoringAdjustments = true;
        GtkNative.gtk_adjustment_set_value(current.horizontalAdjustment, current.pendingScrollX);
        GtkNative.gtk_adjustment_set_value(current.verticalAdjustment, current.pendingScrollY);
        current.restoringAdjustments = false;
        current.scrollRestorePending = false;
        current.centerPending = false;
        current.RefreshVisibleCells();
        return 0;
    }

    static int OnResizeRestore(IntPtr data)
    {
        if (current is null) return 0;
        current.resizeTimerId = 0;
        if (current.mapScroll == IntPtr.Zero) return 0;
        current.restoringAdjustments = true;
        GtkNative.gtk_adjustment_set_value(current.horizontalAdjustment, current.pendingScrollX);
        GtkNative.gtk_adjustment_set_value(current.verticalAdjustment, current.pendingScrollY);
        current.restoringAdjustments = false;
        current.centerPending = false;
        current.RefreshVisibleCells();
        return 0;
    }

    static void OnAdjustmentChanged(IntPtr adjustment, IntPtr data) => current?.OnScrolled();

    void Refresh()
    {
        moneyLabelText();
        RefreshVisibleCells();
        GtkNative.gtk_label_set_text(levelLabel, game.Level.ToString("00"));
        GtkNative.gtk_label_set_text(deliveryLabel, game.Level == FactoryGame.MaxLevel ? "MAX" : $"{game.LevelProgress} / {game.CurrentContract.Total}");
        GtkNative.gtk_label_set_text(progressHeadingLabel, game.Level == FactoryGame.MaxLevel ? "TOATE REȚETELE DEBLOCATE" : $"OBIECTIVE NIVEL {game.Level}: {ContractDescription(game.CurrentContract)}");
        GtkNative.gtk_label_set_text(seedLabel, $"LUME INFINITĂ  ·  #{game.Seed}");
        GtkNative.gtk_label_set_text(worldNameLabel, activeSaveName ?? "Lume nouă");
        GtkNative.gtk_progress_bar_set_fraction(progressBar, game.Level == FactoryGame.MaxLevel ? 1 : (double)game.LevelProgress / Math.Max(1, game.CurrentContract.Total));
        RefreshToolSelection();
    }

    void RefreshVisibleCells()
    {
        if (mapScroll == IntPtr.Zero || horizontalAdjustment == IntPtr.Zero || verticalAdjustment == IntPtr.Zero)
        {
            foreach (var (coordinate, button) in cells) RenderCell(coordinate.x, coordinate.y, button);
            return;
        }

        // The world keeps growing, but only a small rectangle can be seen.
        // Repaint that rectangle plus a one-tile margin instead of walking all
        // GTK buttons ever created for previously visited chunks.
        var tileWidth = (double)tileSize;
        var tileHeight = (double)tileSize;
        var chunkWidth = FactoryGame.Width * tileWidth;
        var chunkHeight = FactoryGame.Height * tileHeight;
        var horizontalPosition = GtkNative.gtk_adjustment_get_value(horizontalAdjustment);
        var verticalPosition = GtkNative.gtk_adjustment_get_value(verticalAdjustment);
        var left = minChunkX * FactoryGame.Width
            + (int)Math.Floor(horizontalPosition / chunkWidth) * FactoryGame.Width
            + (int)Math.Floor((horizontalPosition % chunkWidth) / tileWidth) - 1;
        var top = minChunkY * FactoryGame.Height
            + (int)Math.Floor(verticalPosition / chunkHeight) * FactoryGame.Height
            + (int)Math.Floor((verticalPosition % chunkHeight) / tileHeight) - 1;
        var visibleColumns = (int)Math.Ceiling(GtkNative.gtk_adjustment_get_page_size(horizontalAdjustment) / tileWidth) + 2;
        var visibleRows = (int)Math.Ceiling(GtkNative.gtk_adjustment_get_page_size(verticalAdjustment) / tileHeight) + 2;
        if (visibleColumns <= 2) visibleColumns = 22;
        if (visibleRows <= 2) visibleRows = 14;

        for (var y = top; y < top + visibleRows; y++)
        for (var x = left; x < left + visibleColumns; x++)
            if (cells.TryGetValue((x, y), out var button)) RenderCell(x, y, button);

    }

    void RefreshLoadedTransporters()
    {
        foreach (var (point, button) in cells.ToArray())
            if (game.At(point.x, point.y).Kind is TileKind.Belt or TileKind.Pipe)
                RenderCell(point.x, point.y, button);
    }

    void RenderCell(int x, int y, IntPtr button)
    {
        var tile = game.At(x, y);
        var description = tile.InstalledFluidRecipe is { } fluidRecipe
            ? $"{fluidRecipe.MachineName} · {FluidMachineStatus(tile)}"
            : tile.InstalledRecipe is { } recipe
            ? $"{MachineName(recipe.Id)} · produce {recipe.Product}"
            : tile.Kind is TileKind.Belt or TileKind.Pipe
                ? $"{(tile.Kind == TileKind.Belt ? "Bandă" : "Țeavă")} {Arrow(tile.Direction)}" + (tile.Kind == TileKind.Belt
                    ? tile.BeltItem is { } item ? $" · transportă {ItemName(item)}" : " · goală"
                    : tile.PipeFluid is { } fluid ? $" · transportă {FluidName(fluid)}" : " · goală")
                : TileName(tile.Kind);
        GtkNative.gtk_widget_set_tooltip_text(button, $"{description} · coordonatele {x}, {y}");
        var (label, css) = tile.Kind switch
        {
            TileKind.Empty => ("", "empty"), TileKind.IronDeposit => ("Fe", "iron"), TileKind.CopperDeposit => ("Cu", "copper"),
            TileKind.TinDeposit => ("Sn", "tin"), TileKind.Rock => ("◆", "rock"), TileKind.Tree => ("♣", "tree"),
            TileKind.OilDeposit => ("Oil", "oil"), TileKind.RubberTree => ("🌳", "rubber-tree"), TileKind.LatexCollector => ("L", "latex-collector"),
            TileKind.Miner => ("⛏", "miner"), TileKind.Belt => (Arrow(tile.Direction), "belt"),
            TileKind.Pipe => (Arrow(tile.Direction), "pipe"), TileKind.OilPump => ("⛽", "pump"),
            TileKind.Smelter or TileKind.Assembler or TileKind.AlloySmelter or TileKind.RotorPress => MachineLabel(tile),
            TileKind.Distillery or TileKind.ChemicalReactor or TileKind.Polymerizer or TileKind.GlueMixer => ("⚗", "fluid-machine"),
            TileKind.Ship => ("🚀", "ship"), _ => ("", "empty")
        };
        var frames = SpriteFrames(tile);
        if (frames.Length > 0)
        {
            if (!cellImages.ContainsKey((x, y)))
            {
                GtkNative.gtk_button_set_label(button, "");
                var image = GtkNative.gtk_picture_new_for_filename(frames[0]);
                GtkNative.gtk_picture_set_can_shrink(image, true);
                GtkNative.gtk_widget_set_size_request(image, (int)Math.Round(tileSize * .9), (int)Math.Round(tileSize * .9));
                if (tile.Kind is TileKind.Belt or TileKind.Pipe)
                {
                    var overlay = GtkNative.gtk_overlay_new();
                    GtkNative.gtk_widget_set_hexpand(overlay, true);
                    GtkNative.gtk_widget_set_vexpand(overlay, true);
                    GtkNative.gtk_overlay_set_child(overlay, image);
                    GtkNative.gtk_button_set_child(button, overlay);
                    cellOverlays[(x, y)] = overlay;
                }
                else GtkNative.gtk_button_set_child(button, image);
                cellImages[(x, y)] = image;
                if (frames.Length > 1)
                {
                    cellSpriteFrames[(x, y)] = frames;
                    cellSpriteFrameIndices[(x, y)] = 0;
                    EnsureSpriteTimer();
                }
            }
            if (tile.Kind is TileKind.Belt or TileKind.Pipe)
            {
                var point = (x, y);
                if (!cellOverlays.ContainsKey(point))
                {
                    // Recover if a cell was reattached with its picture but
                    // without the item overlay bookkeeping.
                    var overlay = GtkNative.gtk_overlay_new();
                    GtkNative.gtk_widget_set_hexpand(overlay, true);
                    GtkNative.gtk_widget_set_vexpand(overlay, true);
                    GtkNative.gtk_button_set_child(button, IntPtr.Zero);
                    GtkNative.gtk_overlay_set_child(overlay, cellImages[point]);
                    GtkNative.gtk_button_set_child(button, overlay);
                    cellOverlays[point] = overlay;
                }
                if (tile.Kind == TileKind.Belt) UpdateBeltItemOverlay(point, tile.BeltItem);
                else UpdatePipeFluidOverlay(point, tile.PipeFluid);
            }
        }
        else
        {
            if (cellImages.Remove((x, y))) GtkNative.gtk_button_set_child(button, IntPtr.Zero);
            cellOverlays.Remove((x, y)); beltItemImages.Remove((x, y)); beltItemKinds.Remove((x, y));
            pipeFluidImages.Remove((x, y)); pipeFluidKinds.Remove((x, y));
            cellSpriteFrames.Remove((x, y)); cellSpriteFrameIndices.Remove((x, y));
            GtkNative.gtk_button_set_label(button, label);
            StopSpriteTimerIfIdle();
        }
        SetCellStyle(button, css, tile.Stored > 0 || tile.BeltItem is not null);
    }

    void UpdateBeltItemOverlay((int x, int y) point, ItemKind? item)
    {
        if (!cellOverlays.TryGetValue(point, out var overlay)) return;
        if (item is null || beltItemKinds.GetValueOrDefault(point) != item.Value)
        {
            if (beltItemImages.Remove(point, out var previous)) GtkNative.gtk_overlay_remove_overlay(overlay, previous);
            beltItemKinds.Remove(point);
        }
        if (item is not { } kind || beltItemImages.ContainsKey(point)) return;
        var path = ItemSpritePath(kind);
        if (!File.Exists(path)) return;
        var picture = GtkNative.gtk_picture_new_for_filename(path);
        GtkNative.gtk_picture_set_can_shrink(picture, true);
        var iconSize = BeltItemIconSize(kind);
        GtkNative.gtk_widget_set_size_request(picture, iconSize, iconSize);
        GtkNative.gtk_widget_set_halign(picture, 2); // GTK_ALIGN_END
        GtkNative.gtk_widget_set_valign(picture, 2);
        var inset = (int)Math.Round(tileSize * .04);
        GtkNative.gtk_widget_set_margin_end(picture, inset);
        GtkNative.gtk_widget_set_margin_bottom(picture, inset);
        GtkNative.gtk_widget_add_css_class(picture, "belt-item-texture");
        GtkNative.gtk_overlay_add_overlay(overlay, picture);
        beltItemImages[point] = picture;
        beltItemKinds[point] = kind;
        ApplyBeltItemMotion(point, picture);
        EnsureBeltMotionTimer();
    }

    void UpdatePipeFluidOverlay((int x, int y) point, FluidKind? fluid)
    {
        if (!cellOverlays.TryGetValue(point, out var overlay)) return;
        if (fluid is null || pipeFluidKinds.GetValueOrDefault(point) != fluid.Value)
        {
            if (pipeFluidImages.Remove(point, out var previous)) GtkNative.gtk_overlay_remove_overlay(overlay, previous);
            pipeFluidKinds.Remove(point);
        }
        if (fluid is not { } kind || pipeFluidImages.ContainsKey(point)) return;
        var path = FluidSpritePath(kind);
        if (!File.Exists(path)) return;
        var picture = GtkNative.gtk_picture_new_for_filename(path);
        GtkNative.gtk_picture_set_can_shrink(picture, true);
        var iconSize = (int)Math.Round(tileSize * .64);
        GtkNative.gtk_widget_set_size_request(picture, iconSize, iconSize);
        GtkNative.gtk_widget_set_halign(picture, 3);
        GtkNative.gtk_widget_set_valign(picture, 3);
        GtkNative.gtk_widget_add_css_class(picture, "belt-item-texture");
        GtkNative.gtk_overlay_add_overlay(overlay, picture);
        pipeFluidImages[point] = picture;
        pipeFluidKinds[point] = kind;
    }

    static string FluidSpritePath(FluidKind fluid)
    {
        var file = fluid switch
        {
            FluidKind.Petroleum => "petroleum", FluidKind.Ethanol => "ethanol",
            FluidKind.Ethylene => "ethylene", FluidKind.Polyethylene => "polyethylene", FluidKind.Latex => "latex",
            FluidKind.Glue => "glue", _ => "soldering_alloy"
        };
        return Path.Combine(AppContext.BaseDirectory, "asstets", "fluids", file + ".png");
    }

    void EnsureBeltMotionTimer()
    {
        if (autoRunning && beltItemImages.Count > 0 && beltMotionTimerId == 0)
            beltMotionTimerId = GtkNative.g_timeout_add(33, Marshal.GetFunctionPointerForDelegate(BeltMotionCallback), IntPtr.Zero);
    }

    static int OnBeltMotionTimer(IntPtr data)
    {
        if (current is null || !current.autoRunning || current.beltItemImages.Count == 0)
        {
            if (current is not null) current.beltMotionTimerId = 0;
            return 0;
        }
        current.beltMotionProgress = Math.Min(1, current.beltMotionProgress + 0.033);
        foreach (var (point, picture) in current.beltItemImages.ToArray())
            current.ApplyBeltItemMotion(point, picture);
        return 1;
    }

    void ApplyBeltItemMotion((int x, int y) point, IntPtr picture)
    {
        var tile = game.At(point.x, point.y);
        var item = beltItemKinds.GetValueOrDefault(point);
        var inset = (int)Math.Round(tileSize * .04);
        var offset = inset + (int)Math.Round(Math.Max(0, tileSize - BeltItemIconSize(item) - inset * 2) * beltMotionProgress);
        switch (tile.Direction)
        {
            case Direction.East:
                GtkNative.gtk_widget_set_halign(picture, 0); // GTK_ALIGN_START
                GtkNative.gtk_widget_set_valign(picture, 3); // GTK_ALIGN_CENTER
                GtkNative.gtk_widget_set_margin_start(picture, offset);
                GtkNative.gtk_widget_set_margin_end(picture, 0);
                GtkNative.gtk_widget_set_margin_top(picture, 0);
                GtkNative.gtk_widget_set_margin_bottom(picture, 0);
                break;
            case Direction.West:
                GtkNative.gtk_widget_set_halign(picture, 2); // GTK_ALIGN_END
                GtkNative.gtk_widget_set_valign(picture, 3);
                GtkNative.gtk_widget_set_margin_end(picture, offset);
                GtkNative.gtk_widget_set_margin_start(picture, 0);
                GtkNative.gtk_widget_set_margin_top(picture, 0);
                GtkNative.gtk_widget_set_margin_bottom(picture, 0);
                break;
            case Direction.South:
                GtkNative.gtk_widget_set_halign(picture, 3);
                GtkNative.gtk_widget_set_valign(picture, 0); // GTK_ALIGN_START
                GtkNative.gtk_widget_set_margin_top(picture, offset);
                GtkNative.gtk_widget_set_margin_bottom(picture, 0);
                GtkNative.gtk_widget_set_margin_start(picture, 0);
                GtkNative.gtk_widget_set_margin_end(picture, 0);
                break;
            default: // North
                GtkNative.gtk_widget_set_halign(picture, 3);
                GtkNative.gtk_widget_set_valign(picture, 2); // GTK_ALIGN_END
                GtkNative.gtk_widget_set_margin_bottom(picture, offset);
                GtkNative.gtk_widget_set_margin_top(picture, 0);
                GtkNative.gtk_widget_set_margin_start(picture, 0);
                GtkNative.gtk_widget_set_margin_end(picture, 0);
                break;
        }
    }

    int BeltItemIconSize(ItemKind item)
    {
        var scale = item is ItemKind.CopperOre or ItemKind.CopperPlate or ItemKind.CopperWire ? 0.70 : 0.52;
        return (int)Math.Round(tileSize * scale);
    }

    static string ItemSpritePath(ItemKind item)
    {
        var file = item switch
        {
            ItemKind.IronOre => "iron_ore", ItemKind.CopperOre => "copper_ore", ItemKind.TinOre => "tin_ore",
            ItemKind.IronPlate => "iron_plate", ItemKind.CopperPlate => "copper_plate",
            ItemKind.CopperWire => "copper_wire", ItemKind.Gear => "gear", ItemKind.Circuit => "circuit",
            ItemKind.Frame => "frame", ItemKind.Motor => "motor", ItemKind.AdvancedCircuit => "advanced_circuit",
            ItemKind.Processor => "processor", ItemKind.RocketPart => "rocket_part", ItemKind.Bronze => "bronze",
            ItemKind.Rotor => "rotor", ItemKind.Pump => "pump", ItemKind.Mainframe => "mainframe", _ => "thruster"
        };
        return Path.Combine(AppContext.BaseDirectory, "asstets", "items", file + ".png");
    }

    static string[] SpriteFrames(Tile tile)
    {
        var folder = tile.InstalledFluidRecipe is { } fluidRecipe
            ? FluidMachineSpriteFolder(fluidRecipe.Id)
            : tile.InstalledRecipe is { } recipe
                ? MachineSpriteFolder(recipe.Id)
                : tile.Kind switch
        {
            TileKind.IronDeposit => "iron_deposit",
            TileKind.CopperDeposit => "copper_deposit",
            TileKind.TinDeposit => "tin_deposit",
            TileKind.Tree => "tree",
            TileKind.OilDeposit => "oil_deposit",
            TileKind.Rock => "rock",
            TileKind.Miner => "miner",
            TileKind.Belt => $"belt_{tile.Direction.ToString().ToLowerInvariant()}",
            TileKind.Pipe => $"pipe_{tile.Direction.ToString().ToLowerInvariant()}",
            TileKind.OilPump => "oil_pump",
            TileKind.RubberTree => "rubber_tree", TileKind.LatexCollector => "latex_collector", TileKind.GlueMixer => "glue_mixer",
            TileKind.Distillery => "distillery",
            TileKind.ChemicalReactor => "chemical_reactor",
            TileKind.Polymerizer => "polymerizer",
            TileKind.Smelter => "smelter",
            TileKind.Assembler => "assembler",
            TileKind.Ship => "cargo_ship",
            _ => null
        };
        if (folder is null) return [];
        if (SpriteFrameCache.TryGetValue(folder, out var cached)) return cached;
        var directory = Path.Combine(AppContext.BaseDirectory, "asstets", "sprites", folder);
        var frames = Directory.Exists(directory)
            ? Directory.GetFiles(directory, "frame_*.png").Order(StringComparer.Ordinal).ToArray()
            : [];
        SpriteFrameCache[folder] = frames;
        return frames;
    }

    void EnsureSpriteTimer()
    {
        if (spriteTimerId == 0)
            spriteTimerId = GtkNative.g_timeout_add(180, Marshal.GetFunctionPointerForDelegate(SpriteFrameCallback), IntPtr.Zero);
    }

    void StopSpriteTimerIfIdle()
    {
        if (cellSpriteFrames.Count == 0 && spriteTimerId != 0)
        { GtkNative.g_source_remove(spriteTimerId); spriteTimerId = 0; }
    }

    static int OnSpriteFrameTimer(IntPtr data)
    {
        if (current is null || current.cellSpriteFrames.Count == 0)
        {
            if (current is not null) current.spriteTimerId = 0;
            return 0;
        }
        foreach (var (coordinate, frames) in current.cellSpriteFrames.ToArray())
        {
            if (frames.Length < 2 || !current.cellImages.TryGetValue(coordinate, out var image)) continue;
            var nextFrame = (current.cellSpriteFrameIndices.GetValueOrDefault(coordinate) + 1) % frames.Length;
            GtkNative.gtk_picture_set_filename(image, frames[nextFrame]);
            current.cellSpriteFrameIndices[coordinate] = nextFrame;
        }
        return 1;
    }

    static (string label, string css) MachineLabel(Tile tile) => tile.Kind switch
    {
        TileKind.Smelter => ("♨", "smelter"), TileKind.AlloySmelter => ("♨", "alloy-smelter"),
        TileKind.RotorPress => ("⚙", "rotor-press"), _ => ("▦", "assembler")
    };

    void moneyLabelText()
    { GtkNative.gtk_label_set_text(moneyLabel, $"${game.Money}"); GtkNative.gtk_label_set_text(tickLabel, game.Tick.ToString("000")); }

    void RefreshToolSelection()
    {
        foreach (var (kind, button) in buildButtons)
        {
            var selected = kind == mode
                || (mode == "machine" && kind == $"machine:{selectedRecipeId}")
                || (mode == "fluid_machine" && kind == $"fluid_machine:{selectedFluidRecipeId}");
            if (selected) GtkNative.gtk_widget_add_css_class(button, "selected");
            else GtkNative.gtk_widget_remove_css_class(button, "selected");
        }
        GtkNative.gtk_widget_set_visible(optionsMiner, mode == "miner");
        var hasDirection = mode is "belt" or "pipe" or "pump" or "latex_collector";
        GtkNative.gtk_widget_set_visible(optionsBelt, hasDirection);
        if (hasDirection && directionHeading != IntPtr.Zero)
            GtkNative.gtk_label_set_text(directionHeading, mode == "belt" ? "DIRECȚIA BENZII" : mode == "pump" ? "IEȘIREA POMPEI" : mode == "latex_collector" ? "IEȘIREA COLECTORULUI" : "DIRECȚIA FLUXULUI");
        SetOptionSelection(); UpdateDirectionButtons(GtkNative.gtk_widget_get_first_child(optionsBelt) == IntPtr.Zero ? IntPtr.Zero : GtkNative.gtk_widget_get_next_sibling(GtkNative.gtk_widget_get_first_child(optionsBelt)));
    }

    void SetOptionSelection()
    {
        foreach (var (kind, button) in new[] { (ItemKind.IronOre, oreIronButton), (ItemKind.CopperOre, oreCopperButton), (ItemKind.TinOre, oreTinButton) })
        { if (ore == kind) GtkNative.gtk_widget_add_css_class(button, "selected"); else GtkNative.gtk_widget_remove_css_class(button, "selected"); }
    }

    void UpdateDirectionButtons(IntPtr box)
    {
        if (box == IntPtr.Zero) return;
        var child = GtkNative.gtk_widget_get_first_child(box);
        foreach (var dir in new[] { Direction.North, Direction.East, Direction.South, Direction.West })
        {
            if (child == IntPtr.Zero) break;
            if (direction == dir) GtkNative.gtk_widget_add_css_class(child, "selected"); else GtkNative.gtk_widget_remove_css_class(child, "selected");
            child = GtkNative.gtk_widget_get_next_sibling(child);
        }
    }

    static void SetCellStyle(IntPtr button, string css, bool busy)
    {
        foreach (var name in new[] { "empty", "iron", "copper", "tin", "tree", "rock", "oil", "rubber-tree", "latex-collector", "miner", "belt", "pipe", "pump", "fluid-machine", "smelter", "alloy-smelter", "rotor-press", "assembler", "ship", "busy" }) GtkNative.gtk_widget_remove_css_class(button, name);
        GtkNative.gtk_widget_add_css_class(button, css); if (busy) GtkNative.gtk_widget_add_css_class(button, "busy");
    }

    void SetMessage(string text) => GtkNative.gtk_label_set_text(statusLabel, text);
    static string TileName(TileKind kind) => kind switch
    {
        TileKind.Empty => "Teren liber", TileKind.IronDeposit => "Zăcământ de fier", TileKind.CopperDeposit => "Zăcământ de cupru",
        TileKind.TinDeposit => "Zăcământ de staniu", TileKind.Tree => "Copac", TileKind.OilDeposit => "Zăcământ de petrol", TileKind.Rock => "Bolovan", TileKind.Miner => "Miner",
        TileKind.Belt => "Bandă transportoare", TileKind.Pipe => "Țeavă", TileKind.OilPump => "Pompă de petrol",
        TileKind.Distillery => "Distilerie", TileKind.ChemicalReactor => "Reactor chimic", TileKind.Polymerizer => "Polimerizator",
        TileKind.RubberTree => "Arbore de cauciuc", TileKind.LatexCollector => "Colector de latex", TileKind.GlueMixer => "Malaxor de lipici",
        TileKind.Smelter => "Topitor", TileKind.Assembler => "Asamblor", TileKind.AlloySmelter => "Alloy Smelter",
        TileKind.RotorPress => "Rotor Press", _ => "Navă de marfă"
    };
    static string Arrow(Direction d) => d switch { Direction.North => "↑", Direction.East => "→", Direction.South => "↓", _ => "←" };
    static string ItemGlyph(ItemKind? i) => i switch
    {
        ItemKind.IronOre => "Fe", ItemKind.CopperOre => "Cu", ItemKind.IronPlate => "FeP", ItemKind.CopperPlate => "CuP", ItemKind.TinOre => "Sn",
        ItemKind.CopperWire => "Fi", ItemKind.Gear => "An", ItemKind.Circuit => "Ci", ItemKind.Frame => "Ca",
        ItemKind.Motor => "Mo", ItemKind.AdvancedCircuit => "C+", ItemKind.Processor => "Pr", ItemKind.RocketPart => "Ra",
        ItemKind.Bronze => "Br", ItemKind.Rotor => "Ro", ItemKind.Pump => "Pu", ItemKind.Mainframe => "Mf", ItemKind.Thruster => "Th", _ => "·"
    };

    IntPtr Button(string text, string? css = null)
    { var button = GtkNative.gtk_button_new_with_label(text); if (css is not null) GtkNative.gtk_widget_add_css_class(button, css); return button; }
    static IntPtr Label(string text, string? css = null)
    { var label = GtkNative.gtk_label_new(text); if (css is not null) GtkNative.gtk_widget_add_css_class(label, css); return label; }
    IntPtr Box(int orientation, double spacingPercent)
    {
        var box = GtkNative.gtk_box_new(orientation, ResponsiveUnit(spacingPercent));
        responsiveGaps[box] = spacingPercent;
        return box;
    }
    static void Append(IntPtr box, IntPtr child) => GtkNative.gtk_box_append(box, child);
    void Margins(IntPtr widget, double marginPercent)
    {
        responsiveMargins[widget] = marginPercent;
        var margin = ResponsiveUnit(marginPercent);
        GtkNative.gtk_widget_set_margin_start(widget, margin); GtkNative.gtk_widget_set_margin_end(widget, margin);
        GtkNative.gtk_widget_set_margin_top(widget, margin); GtkNative.gtk_widget_set_margin_bottom(widget, margin);
    }
    void Connect(IntPtr widget, Action action) { Actions[widget] = action; viewActions.Add(widget); GtkNative.g_signal_connect_data(widget, "clicked", Marshal.GetFunctionPointerForDelegate(ClickCallback), IntPtr.Zero, IntPtr.Zero, 0); }
    static void OnClicked(IntPtr widget, IntPtr data) { if (Actions.TryGetValue(widget, out var action)) action(); }
    static int OnKey(IntPtr controller, uint keyval, uint keycode, uint state, IntPtr data)
    {
        if (current is null || current.mapScroll == IntPtr.Zero) return 0;
        if (keyval is 0x77 or 0x57 or 0x61 or 0x41 or 0x73 or 0x53 or 0x64 or 0x44)
        {
            // GTK emits key-pressed repeatedly while a key is held. Movement
            // is cell-based, so accept one move until the matching key release.
            if (!current.pressedMovementKeys.Add(keyval)) return 1;
            var step = (double)current.tileSize;
            var horizontal = GtkNative.gtk_adjustment_get_value(current.horizontalAdjustment);
            var vertical = GtkNative.gtk_adjustment_get_value(current.verticalAdjustment);
            var deltaX = 0d; var deltaY = 0d;
            if (keyval is 0x77 or 0x57) deltaY = -step;
            else if (keyval is 0x73 or 0x53) deltaY = step;
            else if (keyval is 0x61 or 0x41) deltaX = -step;
            else deltaX = step;

            if (current.centerPending || current.scrollRestorePending)
            {
                current.pendingScrollX += deltaX;
                current.pendingScrollY += deltaY;
            }
            else if (deltaX != 0)
                GtkNative.gtk_adjustment_set_value(current.horizontalAdjustment, horizontal + deltaX);
            else if (deltaY != 0)
                GtkNative.gtk_adjustment_set_value(current.verticalAdjustment, vertical + deltaY);
            return 1;
        }
        if (keyval == 0x20) { current.Advance(); return 1; }
        if (keyval == 0xff1b) { current.mode = null; current.RefreshToolSelection(); current.SetMessage("Selectează o clădire sau inspectează harta."); return 1; }
        return 0;
    }

    static void OnKeyReleased(IntPtr controller, uint keyval, uint keycode, uint state, IntPtr data)
        => current?.pressedMovementKeys.Remove(keyval);
    static int OnClose(IntPtr window, IntPtr data)
    {
        if (current is not null)
        {
            current.SaveCurrent(); current.StopAuto();
            if (current.spriteTimerId != 0) GtkNative.g_source_remove(current.spriteTimerId);
            if (current.resizeTimerId != 0) GtkNative.g_source_remove(current.resizeTimerId);
            if (current.scrollRestoreTimerId != 0) GtkNative.g_source_remove(current.scrollRestoreTimerId);
            if (current.musicStream != IntPtr.Zero)
            {
                GtkNative.gtk_media_stream_pause(current.musicStream);
                GtkNative.g_object_unref(current.musicStream);
                current.musicStream = IntPtr.Zero;
            }
        }
        GtkNative.g_main_loop_quit(mainLoop); return 0;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void SignalCallback(IntPtr widget, IntPtr data);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int KeyCallback(IntPtr controller, uint keyval, uint keycode, uint state, IntPtr data);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void KeyReleaseCallback(IntPtr controller, uint keyval, uint keycode, uint state, IntPtr data);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int CloseCallback(IntPtr window, IntPtr data);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int TimerCallback(IntPtr data);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int IdleCallback(IntPtr data);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void NotifyCallback(IntPtr instance, IntPtr property, IntPtr data);
}

static class GtkNative
{
    const string Gtk = "libgtk-4.so.1", GObject = "libgobject-2.0.so.0", Gio = "libgio-2.0.so.0", Glib = "libglib-2.0.so.0";
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_init();
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern int gtk_init_check();
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr gtk_window_new();
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_window_set_title(IntPtr window, [MarshalAs(UnmanagedType.LPUTF8Str)] string title);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_window_set_child(IntPtr window, IntPtr child);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_window_present(IntPtr window);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_window_maximize(IntPtr window);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_window_close(IntPtr window);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr gtk_box_new(int orientation, int spacing);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_box_set_spacing(IntPtr box, int spacing);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_box_append(IntPtr box, IntPtr child);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr gtk_label_new([MarshalAs(UnmanagedType.LPUTF8Str)] string text);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_label_set_text(IntPtr label, [MarshalAs(UnmanagedType.LPUTF8Str)] string text);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_label_set_xalign(IntPtr label, float xalign);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_label_set_wrap(IntPtr label, [MarshalAs(UnmanagedType.Bool)] bool wrap);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr gtk_button_new_with_label([MarshalAs(UnmanagedType.LPUTF8Str)] string label);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_button_set_label(IntPtr button, [MarshalAs(UnmanagedType.LPUTF8Str)] string label);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_button_set_child(IntPtr button, IntPtr child);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr gtk_entry_new();
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_entry_set_placeholder_text(IntPtr entry, [MarshalAs(UnmanagedType.LPUTF8Str)] string text);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr gtk_editable_get_text(IntPtr editable);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_editable_set_text(IntPtr editable, [MarshalAs(UnmanagedType.LPUTF8Str)] string text);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr gtk_picture_new_for_filename([MarshalAs(UnmanagedType.LPUTF8Str)] string filename);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr gtk_overlay_new();
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_overlay_set_child(IntPtr overlay, IntPtr child);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_overlay_add_overlay(IntPtr overlay, IntPtr child);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_overlay_remove_overlay(IntPtr overlay, IntPtr child);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_picture_set_filename(IntPtr picture, [MarshalAs(UnmanagedType.LPUTF8Str)] string filename);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_picture_set_can_shrink(IntPtr picture, [MarshalAs(UnmanagedType.Bool)] bool canShrink);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr gtk_media_file_new_for_filename([MarshalAs(UnmanagedType.LPUTF8Str)] string filename);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_media_file_set_filename(IntPtr mediaFile, [MarshalAs(UnmanagedType.LPUTF8Str)] string filename);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_media_stream_play(IntPtr stream);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_media_stream_pause(IntPtr stream);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.Bool)] public static extern bool gtk_media_stream_get_ended(IntPtr stream);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_media_stream_set_loop(IntPtr stream, [MarshalAs(UnmanagedType.Bool)] bool loop);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_media_stream_set_volume(IntPtr stream, double volume);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr gtk_grid_new();
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr gtk_scrolled_window_new();
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_scrolled_window_set_child(IntPtr scrolledWindow, IntPtr child);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_scrolled_window_set_policy(IntPtr scrolledWindow, int horizontalPolicy, int verticalPolicy);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr gtk_scrolled_window_get_hadjustment(IntPtr scrolledWindow);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr gtk_scrolled_window_get_vadjustment(IntPtr scrolledWindow);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern double gtk_adjustment_get_value(IntPtr adjustment);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern double gtk_adjustment_get_upper(IntPtr adjustment);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern double gtk_adjustment_get_page_size(IntPtr adjustment);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_adjustment_set_value(IntPtr adjustment, double value);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_grid_remove(IntPtr grid, IntPtr child);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_grid_attach(IntPtr grid, IntPtr child, int column, int row, int width, int height);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_grid_set_row_spacing(IntPtr grid, uint spacing);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_grid_set_column_spacing(IntPtr grid, uint spacing);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_grid_set_row_homogeneous(IntPtr grid, [MarshalAs(UnmanagedType.Bool)] bool homogeneous);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_grid_set_column_homogeneous(IntPtr grid, [MarshalAs(UnmanagedType.Bool)] bool homogeneous);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr gtk_progress_bar_new();
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_progress_bar_set_fraction(IntPtr progress, double fraction);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr gtk_css_provider_new();
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_css_provider_load_from_data(IntPtr provider, [MarshalAs(UnmanagedType.LPUTF8Str)] string data, nint length);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_style_context_add_provider_for_display(IntPtr display, IntPtr provider, uint priority);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr gdk_display_get_default();
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_widget_add_css_class(IntPtr widget, [MarshalAs(UnmanagedType.LPUTF8Str)] string cssClass);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_widget_remove_css_class(IntPtr widget, [MarshalAs(UnmanagedType.LPUTF8Str)] string cssClass);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_widget_set_size_request(IntPtr widget, int width, int height);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern int gtk_widget_get_width(IntPtr widget);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern int gtk_widget_get_height(IntPtr widget);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_widget_set_hexpand(IntPtr widget, [MarshalAs(UnmanagedType.Bool)] bool expand);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_widget_set_vexpand(IntPtr widget, [MarshalAs(UnmanagedType.Bool)] bool expand);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_widget_set_halign(IntPtr widget, int align);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_widget_set_valign(IntPtr widget, int align);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_widget_set_visible(IntPtr widget, [MarshalAs(UnmanagedType.Bool)] bool visible);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_widget_set_tooltip_text(IntPtr widget, [MarshalAs(UnmanagedType.LPUTF8Str)] string text);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_widget_set_margin_start(IntPtr widget, int margin);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_widget_set_margin_end(IntPtr widget, int margin);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_widget_set_margin_top(IntPtr widget, int margin);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_widget_set_margin_bottom(IntPtr widget, int margin);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr gtk_widget_get_first_child(IntPtr widget);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr gtk_widget_get_next_sibling(IntPtr widget);
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr gtk_event_controller_key_new();
    [DllImport(Gtk, CallingConvention = CallingConvention.Cdecl)] public static extern void gtk_widget_add_controller(IntPtr widget, IntPtr controller);
    [DllImport(GObject, CallingConvention = CallingConvention.Cdecl)] public static extern ulong g_signal_connect_data(IntPtr instance, [MarshalAs(UnmanagedType.LPUTF8Str)] string signal, IntPtr callback, IntPtr data, IntPtr destroyData, int flags);
    [DllImport(GObject, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr g_object_ref(IntPtr instance);
    [DllImport(GObject, CallingConvention = CallingConvention.Cdecl)] public static extern void g_object_unref(IntPtr instance);
    [DllImport(Gio, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr g_main_loop_new(IntPtr context, [MarshalAs(UnmanagedType.Bool)] bool running);
    [DllImport(Gio, CallingConvention = CallingConvention.Cdecl)] public static extern void g_main_loop_run(IntPtr loop);
    [DllImport(Gio, CallingConvention = CallingConvention.Cdecl)] public static extern void g_main_loop_quit(IntPtr loop);
    [DllImport(Gio, CallingConvention = CallingConvention.Cdecl)] public static extern void g_main_loop_unref(IntPtr loop);
    [DllImport(Glib, CallingConvention = CallingConvention.Cdecl)] public static extern uint g_timeout_add(uint interval, IntPtr function, IntPtr data);
    [DllImport(Glib, CallingConvention = CallingConvention.Cdecl)] public static extern int g_source_remove(uint tag);
}
