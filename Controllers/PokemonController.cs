// Importación de librerías necesarias
using Microsoft.AspNetCore.Mvc;      // Controladores y vistas de ASP.NET MVC
using ClosedXML.Excel;               // Librería para crear y manipular archivos Excel (.xlsx)
using MailKit.Net.Smtp;              // Cliente SMTP para enviar correos
using MimeKit;                       // Construcción de mensajes de correo (texto, adjuntos, etc.)
using Pokemon.Services;              // Servicios personalizados del proyecto (capa de lógica)
using Pokemon.Models;                // Modelos de datos (POCOs)

// Controlador principal que maneja las vistas y acciones relacionadas con Pokémon
public class PokemonController : Controller
{
    // Inyección de dependencias: se recibe el servicio que llama a la API y la configuración general
    private readonly IPokeApiService _api;     // Servicio para consultar datos desde la PokeAPI
    private readonly IConfiguration _config;   // Acceso al archivo de configuración appsettings.json

    // Constructor que inyecta las dependencias cuando se crea el controlador
    public PokemonController(IPokeApiService api, IConfiguration config)
    {
        _api = api;
        _config = config;
    }

    // Método principal que muestra la lista de Pokémon con paginación y filtros
    public async Task<IActionResult> Index(int page = 1, int pageSize = 20, string? name = null, string? species = null)
    {
        // Se obtiene la lista completa de especies (para el filtro desplegable)
        var speciesList = await _api.GetAllSpeciesAsync();

        // Se obtiene la lista de Pokémon paginada y filtrada desde la API
        var (items, total) = await _api.GetPokemonPageAsync(page, pageSize, name, species);

        // Se guardan variables en el ViewBag (disponibles en la vista Razor)
        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        ViewBag.Total = total;
        ViewBag.NameFilter = name;
        ViewBag.SpeciesFilter = species;

        // Se limita la cantidad de especies mostradas en el dropdown (solo 20)
        ViewBag.SpeciesList = speciesList.Take(20).ToList();

        // Se devuelve la vista principal con la lista de Pokémon (items)
        return View(items);
    }

    // Acción que devuelve los detalles de un Pokémon específico
    public async Task<IActionResult> Details(string name)
    {
        // Llama al servicio para obtener los detalles del Pokémon por nombre
        var det = await _api.GetPokemonDetailsAsync(name);

        // Validación: si no se encuentra, responde con 404 NotFound
        if (det == null) return NotFound();

        // Devuelve una vista parcial (modal) con los datos del Pokémon
        return PartialView("_DetailsPartial", det);
    }

    // Acción HTTP POST para exportar la lista filtrada de Pokémon a un archivo Excel
    [HttpGet]
    public async Task<IActionResult> ExportExcel(string? name, string? species, int page = 1, int pageSize = 20)
    {
        try
        {
            // Registra en consola los filtros y paginación usados
            Console.WriteLine($"Exportando Excel con filtros: name={name}, species={species}, page={page}, pageSize={pageSize}");

            // Obtiene la lista de Pokémon según los filtros y paginación indicados
            var (items, _) = await _api.GetPokemonPageAsync(page, pageSize, name, species);

            Console.WriteLine($"Pokémon recibidos: {items.Count()}");

            // Crea un nuevo libro de Excel y una hoja llamada "Pokemons"
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Pokemons");

            ws.Cell(1, 1).Value = "Name";
            ws.Cell(1, 2).Value = "Species";
            ws.Cell(1, 3).Value = "ImageUrl";

            int r = 2;
            foreach (var it in items)
            {
                ws.Cell(r, 1).Value = it.Name;
                ws.Cell(r, 2).Value = it.SpeciesName ?? "";
                ws.Cell(r, 3).Value = it.ImageUrl ?? "";
                r++;
            }

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            ms.Position = 0;

            return File(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "pokemons.xlsx");
        }
        catch (Exception ex)
        {
            Console.WriteLine("❌ Error en ExportExcel: " + ex.ToString());
            return StatusCode(500, "Error generando el Excel: " + ex.Message);
        }
    }

    // Acción HTTP POST que genera un archivo Excel y lo envía por correo electrónico
    [HttpPost]
    public async Task<IActionResult> SendEmail(
    string toEmail,
    string? name,
    string? species,
    int page = 1,
    int pageSize = 20
)
    {
        try
        {
            Console.WriteLine($"📧 Enviando correo con filtros: name={name}, species={species}, page={page}, pageSize={pageSize}");

            // 📝 1. Exportar solo los Pokémon visibles
            var (items, _) = await _api.GetPokemonPageAsync(page, pageSize, name, species);

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Pokemons");

            ws.Cell(1, 1).Value = "Name";
            ws.Cell(1, 2).Value = "Species";
            ws.Cell(1, 3).Value = "ImageUrl";

            int r = 2;
            foreach (var it in items)
            {
                ws.Cell(r, 1).Value = it.Name;
                ws.Cell(r, 2).Value = it.SpeciesName ?? "";
                ws.Cell(r, 3).Value = it.ImageUrl ?? "";
                r++;
            }

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            ms.Position = 0;

            // ✉️ 2. Configurar el correo
            var smtp = _config.GetSection("SmtpSettings").Get<SmtpSettings>();

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Pokemon", smtp.From));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = "Pokemons export";

            var builder = new BodyBuilder { TextBody = "Attached list of pokemons." };
            builder.Attachments.Add("pokemons.xlsx", ms.ToArray(), new ContentType("application", "vnd.openxmlformats-officedocument.spreadsheetml.sheet"));
            message.Body = builder.ToMessageBody();

            // 📤 3. Enviar correo
            using var client = new SmtpClient();
            await client.ConnectAsync(smtp.Host, smtp.Port, MailKit.Security.SecureSocketOptions.StartTlsWhenAvailable);

            if (!string.IsNullOrEmpty(smtp.User))
                await client.AuthenticateAsync(smtp.User, smtp.Pass);

            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            Console.WriteLine("✅ Correo enviado correctamente.");
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            Console.WriteLine("❌ Error enviando correo: " + ex);
            return StatusCode(500, "Error enviando correo: " + ex.Message);
        }
    }

}
