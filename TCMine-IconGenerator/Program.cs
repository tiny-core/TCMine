using TCMine_IconGenerator;

// ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
// Gera os recursos visuais do TCMine para AMBOS os projetos, a partir do mesmo
// logótipo (cubo isométrico laranja) — para a estética ficar consistente:
//
//   Launcher → TCMine-Launcher/Assets/
//       icon.png (256) · icon.ico (multi-res) · splash.png (520×300, instalador Velopack)
//   Server → TCMine-Server/wwwroot/
//       favicon.ico (16/32/48) · favicon.png (32) · logo.png (256) · og-image.png (1200 × 630)
//
// A raiz do repositório é detectada automaticamente (procura a *.sln subindo nas
// pastas), correndo SEM argumentos — basta dar Run no Rider. Opcional: passar
// a raiz como primeiro argumento.
// ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

var root = Utility.FindRepoRoot(args.Length > 0 ? args[0] : null);
var launcherAssets = Path.Combine(root, "TCMine-Launcher", "Assets");
var serverWww = Path.Combine(root, "TCMine-Server", "wwwroot");
var serverImages = Path.Combine(root, "TCMine-Server", "wwwroot", "Images");

Directory.CreateDirectory(launcherAssets);
// Directory.CreateDirectory(serverWww);
// Directory.CreateDirectory(serverImages);

int[] sizes = [16, 32, 48, 64, 128, 256];
var pngs = sizes.ToDictionary(s => s, Utility.RenderIcon);

// ── Launcher ─────────────────────────────────────────────────────────────────────────────────────────────────────────
File.WriteAllBytes(Path.Combine(launcherAssets, "icon.png"), pngs[256]);
Utility.WriteIco(Path.Combine(launcherAssets, "icon.ico"), pngs);
File.WriteAllBytes(Path.Combine(launcherAssets, "splash.png"), 
    Utility.RenderSplash(
        "Segoe UI",
        "TCMine Launcher", 
        "A preparar a instalação…",
        520, 300));

Console.WriteLine($"Launcher → {launcherAssets}");
Console.WriteLine("  icon.png · icon.ico · splash.png");

// ── Server ──────────────────────────────────────────────────────────────────
Utility.WriteIco(Path.Combine(serverWww, "favicon.ico"),
    new Dictionary<int, byte[]> { [16] = pngs[16], [32] = pngs[32], [48] = pngs[48] });

File.WriteAllBytes(Path.Combine(serverImages, "favicon.png"), pngs[32]);
File.WriteAllBytes(Path.Combine(serverImages, "logo.png"), pngs[256]);
File.WriteAllBytes(Path.Combine(serverImages, "og-image.png"), 
    Utility.RenderBanner(
        "Segoe UI",
        "TCMine Launcher", 
        "Descarrega e joga",
        1200, 630));

Console.WriteLine($"Server   → {serverWww}");
Console.WriteLine("  favicon.ico · favicon.png · logo.png · og-image.png");
