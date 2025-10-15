// Namespace del servicio principal donde se implementan las llamadas a la PokeAPI
namespace Pokemon.Services;

using System.Net.Http.Json;             // Extensiones para consumir JSON fácilmente con HttpClient
using Microsoft.Extensions.Caching.Memory; // Caché en memoria (para optimizar llamadas repetidas)
using Pokemon.Models;                   // Modelos de datos usados para mapear las respuestas

// ----------------------------------------------------
// INTERFAZ DEL SERVICIO
// ----------------------------------------------------
// Define el contrato (qué métodos ofrece el servicio) sin implementación.
// Esto permite inyectar e intercambiar implementaciones fácilmente.
public interface IPokeApiService
{
    // Obtiene una lista paginada de Pokémon junto con el total disponible.
    Task<(IEnumerable<PokemonSummary> Items, int Total)> GetPokemonPageAsync(int page, int pageSize, string? nameFilter, string? speciesFilter);

    // Obtiene la lista completa de especies de Pokémon (para el filtro del combo).
    Task<IEnumerable<string>> GetAllSpeciesAsync();

    // Obtiene los detalles completos de un Pokémon por nombre.
    Task<PokemonDetails?> GetPokemonDetailsAsync(string name);
}

// ----------------------------------------------------
// IMPLEMENTACIÓN DEL SERVICIO
// ----------------------------------------------------
public class PokeApiService : IPokeApiService
{
    private readonly HttpClient _http;      // Cliente HTTP configurado con la URL base de la API
    private readonly IMemoryCache _cache;   // Caché en memoria para guardar resultados temporales

    // Constructor que recibe dependencias vía inyección
    public PokeApiService(HttpClient http, IMemoryCache cache)
    {
        _http = http;
        _cache = cache;
    }

    // ----------------------------------------------------
    // MÉTODO: GetPokemonPageAsync
    // ----------------------------------------------------
    // Obtiene una lista paginada de Pokémon desde la API pública.
    // Permite filtrar por nombre y especie.
    public async Task<(IEnumerable<PokemonSummary> Items, int Total)> GetPokemonPageAsync(int page, int pageSize, string? nameFilter, string? speciesFilter)
    {
        // Calcula el desplazamiento (offset) para la paginación
        int offset = (page - 1) * pageSize;

        // Llama a la API de Pokémon con límite y desplazamiento
        var list = await _http.GetFromJsonAsync<PokemonListResponse>($"pokemon?limit={pageSize}&offset={offset}");

        // Si la respuesta es nula, inicializa una lista vacía para evitar errores
        var results = list?.results ?? new List<PokemonNameUrl>();

        // Lista que contendrá los Pokémon ya procesados
        var summaries = new List<PokemonSummary>();

        // 🔁 Ciclo foreach: recorre cada resultado del listado básico de Pokémon
        foreach (var p in results)
        {
            // Validación: si hay un filtro de nombre, ignora los que no coincidan
            if (!string.IsNullOrEmpty(nameFilter) &&
                !p.name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                // Obtiene los detalles individuales de cada Pokémon
                var detail = await GetPokemonDetailsAsync(p.name);
                if (detail == null) continue;

                // Validación: si hay filtro de especie, ignora los que no coincidan
                if (!string.IsNullOrEmpty(speciesFilter) &&
                    (detail.SpeciesName == null ||
                     !detail.SpeciesName.Contains(speciesFilter, StringComparison.OrdinalIgnoreCase)))
                    continue;

                // Agrega el Pokémon a la lista resumen
                summaries.Add(new PokemonSummary
                {
                    Name = detail.Name,
                    SpeciesName = detail.SpeciesName,
                    ImageUrl = detail.ImageUrl
                });
            }
            catch
            {
                // Si una llamada individual falla (por red o JSON), se ignora ese Pokémon
                continue;
            }
        }

        // Llama de nuevo a la API solo para obtener el total de Pokémon disponibles
        var root = await _http.GetFromJsonAsync<PokemonListResponse>($"pokemon?limit=1&offset=0");
        int total = root?.count ?? summaries.Count;

        // Devuelve la lista paginada y el total
        return (summaries, total);
    }

    // ----------------------------------------------------
    // MÉTODO: GetAllSpeciesAsync
    // ----------------------------------------------------
    // Obtiene todas las especies de Pokémon (para llenar el dropdown del filtro)
    // y las guarda temporalmente en caché para no consultar repetidamente.
    public async Task<IEnumerable<string>> GetAllSpeciesAsync()
    {
        // Busca en caché la lista; si no existe, la crea mediante el delegado async
        return await _cache.GetOrCreateAsync("species_list", async entry =>
        {
            // Define cuánto tiempo estará disponible en caché (6 horas)
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6);

            // Llama a la API que devuelve todas las especies
            var resp = await _http.GetFromJsonAsync<SpeciesListResponse>("pokemon-species?limit=10000");

            // Si hay resultados, extrae los nombres; si no, devuelve lista vacía
            return resp?.results?.Select(s => s.name) ?? Enumerable.Empty<string>();

        }) ?? Enumerable.Empty<string>(); // Si la caché devuelve null, se asegura devolver algo
    }

    // ----------------------------------------------------
    // MÉTODO: GetPokemonDetailsAsync
    // ----------------------------------------------------
    // Obtiene la información detallada de un Pokémon (altura, peso, sprite, especie, etc.)
    // También utiliza caché para mejorar rendimiento.
    public async Task<PokemonDetails?> GetPokemonDetailsAsync(string name)
    {
        // Intenta recuperar desde la caché la información de este Pokémon
        return await _cache.GetOrCreateAsync($"pokemon_{name}", async entry =>
        {
            // Se mantiene en caché por 30 minutos
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);

            // Llama a la API: /pokemon/{name}
            var detail = await _http.GetFromJsonAsync<PokemonDetailResponse>($"pokemon/{name}");
            if (detail == null) return null;

            // También se necesita la especie, que requiere otra llamada
            string speciesName = "";
            try
            {
                var species = await _http.GetFromJsonAsync<PokemonSpeciesRes>($"pokemon-species/{detail.id}");
                speciesName = species?.name ?? "";
            }
            catch
            {
                // Si la llamada de especie falla, se deja vacío el nombre
                speciesName = "";
            }

            // Retorna un objeto con los datos consolidados del Pokémon
            return new PokemonDetails
            {
                Name = detail.name,
                ImageUrl = detail.sprites?.front_default,
                Height = detail.height,
                Weight = detail.weight,
                SpeciesName = speciesName
            };
        });
    }

    // ----------------------------------------------------
    // CLASES PRIVADAS DE APOYO
    // ----------------------------------------------------
    // Estas clases internas modelan la estructura JSON que devuelve la PokeAPI.
    // Son necesarias para que HttpClient pueda deserializar correctamente la respuesta.

    private class PokemonListResponse
    {
        public int count { get; set; }                          // Total de Pokémon disponibles
        public string? next { get; set; }                       // URL a la siguiente página (si aplica)
        public string? previous { get; set; }                   // URL a la página anterior (si aplica)
        public List<PokemonNameUrl> results { get; set; } = new(); // Lista básica de Pokémon (nombre + URL)
    }

    private class PokemonNameUrl
    {
        public string name { get; set; } = null!;               // Nombre del Pokémon
        public string url { get; set; } = null!;                // Enlace a los detalles
    }

    private class PokemonDetailResponse
    {
        public int id { get; set; }                             // ID del Pokémon
        public string name { get; set; } = null!;               // Nombre
        public int height { get; set; }                         // Altura
        public int weight { get; set; }                         // Peso
        public Sprites? sprites { get; set; }                   // Imagen principal
        public List<AbilityInfo> abilities { get; set; } = new(); // Habilidades
        public List<TypeInfo> types { get; set; } = new();        // Tipos
    }

    private class Sprites
    {
        public string? front_default { get; set; }              // URL de la imagen frontal
    }

    private class AbilityInfo
    {
        public Ability ability { get; set; } = new();           // Información de una habilidad
    }

    private class Ability
    {
        public string name { get; set; } = null!;               // Nombre de la habilidad
        public string url { get; set; } = null!;                // Enlace con más información
    }

    private class TypeInfo
    {
        public TypeDetail type { get; set; } = new();           // Información del tipo del Pokémon
    }

    private class TypeDetail
    {
        public string name { get; set; } = null!;               // Nombre del tipo (agua, fuego, etc.)
        public string url { get; set; } = null!;                // URL al tipo
    }

    private class SpeciesListResponse
    {
        public List<PokemonNameUrl>? results { get; set; }      // Lista de especies
    }

    private class PokemonSpeciesRes
    {
        public string name { get; set; } = null!;               // Nombre de la especie (por ejemplo “bulbasaur”)
    }
}
