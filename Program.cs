// ----------------------------------------------------
// IMPORTACIÓN DE DEPENDENCIAS
// ----------------------------------------------------

// Permite usar la caché en memoria (para guardar resultados temporalmente)
using Microsoft.Extensions.Caching.Memory;

// Espacio de nombres donde está definido el servicio que consume la API de Pokémon
using Pokemon.Services;

// Espacio de nombres de los modelos de configuración y datos
using Pokemon.Models;

// ----------------------------------------------------
// CONFIGURACIÓN INICIAL DEL SERVIDOR WEB
// ----------------------------------------------------

// Crea un objeto "builder" que se encarga de configurar la aplicación web.
// Incluye el servidor Kestrel, configuración, dependencias y servicios de ASP.NET Core.
var builder = WebApplication.CreateBuilder(args);

// ----------------------------------------------------
// REGISTRO DE SERVICIOS EN EL CONTENEDOR DE DEPENDENCIAS (DI)
// ----------------------------------------------------

// Habilita el uso de controladores y vistas Razor (MVC completo)
builder.Services.AddControllersWithViews();

// Configura un HttpClient para inyectar en PokeApiService.
// Esto permite que PokeApiService realice solicitudes HTTP a la PokeAPI.
builder.Services.AddHttpClient<IPokeApiService, PokeApiService>(client =>
{
    // Se define la URL base para todas las peticiones (no es necesario repetirla en cada método).
    client.BaseAddress = new Uri("https://pokeapi.co/api/v2/");
});

// Habilita un sistema de caché en memoria compartido en toda la aplicación.
// Este caché se utiliza en PokeApiService para reducir llamadas repetidas a la API.
builder.Services.AddMemoryCache();

// Configura el sistema para leer la sección "SmtpSettings" desde appsettings.json.
// Los datos (host, puerto, usuario, contraseña) se inyectan donde se use SmtpSettings.
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("SmtpSettings"));

// ----------------------------------------------------
// CONSTRUCCIÓN DE LA APLICACIÓN
// ----------------------------------------------------

// Crea la instancia final de la aplicación con toda la configuración anterior.
var app = builder.Build();

// ----------------------------------------------------
// CONFIGURACIÓN DEL PIPELINE DE MIDDLEWARE (FLUJO DE PETICIONES HTTP)
// ----------------------------------------------------

// Si la aplicación NO está en entorno de desarrollo, se usa un manejador de errores.
// Esto evita mostrar detalles técnicos al usuario final y redirige a /Home/Error.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

// Habilita el acceso a archivos estáticos (CSS, JS, imágenes) desde la carpeta wwwroot.
app.UseStaticFiles();

// Habilita el enrutamiento, necesario para mapear URLs a controladores y acciones.
app.UseRouting();

// ----------------------------------------------------
// CONFIGURACIÓN DE LAS RUTAS PRINCIPALES (ENDPOINTS)
// ----------------------------------------------------

// Define la ruta predeterminada del sitio web.
// Si el usuario entra a la raíz "/", se ejecuta el controlador "Pokemon" y la acción "Index".
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Pokemon}/{action=Index}/{id?}"
);

// ----------------------------------------------------
// INICIO DE LA APLICACIÓN
// ----------------------------------------------------

// Lanza el servidor Kestrel y comienza a escuchar peticiones HTTP.
// A partir de aquí, la aplicación está en ejecución.
app.Run();
