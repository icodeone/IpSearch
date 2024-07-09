using System.Text.Json;
using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Responses;

namespace IpSearch;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddAuthorization();

        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseAuthorization();
        GeoIpSearch.Init();
        app.MapGet("/", (HttpContext httpContext) =>
            {
                var request = httpContext.Request;
                var response = httpContext.Response;

                var ip = request.Query["ip"].ToString();
                if (string.IsNullOrEmpty(ip)) ip = GetIpAddress(request.HttpContext);

                var cityResult = new CityResponse();
                try
                {
                    cityResult = GeoIpSearch.CityReader.City(ip);
                }
                catch (Exception)
                {
                }

                var asnResult = new AsnResponse();
                try
                {
                    asnResult = GeoIpSearch.ASNReader.Asn(ip);
                }
                catch (Exception)
                {
                }

                var model = new GeoIpResult
                {
                    ip = ip,
                    longitude = cityResult?.Location?.Longitude ?? 0.0,
                    latitude = cityResult?.Location?.Latitude ?? 0.0,
                    country_code = cityResult?.Country?.IsoCode ?? "",
                    country = cityResult?.Country?.Name ?? "",
                    timezone = cityResult?.Location?.TimeZone ?? "",
                    asn = asnResult?.AutonomousSystemNumber ?? 0,
                    organization = asnResult?.AutonomousSystemOrganization ?? "",
                    language = CountryCodeToLanguage.GetLanguageByCountryCode(cityResult?.Country?.IsoCode ?? "")
                };

                var callback = request.Query["callback"].ToString();
                if (string.IsNullOrEmpty(callback))
                {
                    response.ContentType = "application/json";
                    return JsonSerializer.Serialize(model);
                }

                var json = JsonSerializer.Serialize(model);
                var jsonp = $"{callback}({json})";
                response.ContentType = "application/javascript";
                return jsonp;
            })
            .WithOpenApi();

        string GetIpAddress(HttpContext context)
        {
            var xRealIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
            var xForwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();

            if (!string.IsNullOrEmpty(xRealIp)) return xRealIp;

            if (!string.IsNullOrEmpty(xForwardedFor))
            {
                var ip = xForwardedFor.Split(',').FirstOrDefault();
                if (!string.IsNullOrEmpty(ip)) return ip;
            }

            return context.Connection.RemoteIpAddress?.ToString();
        }

        app.Run();
    }
}

public class GeoIpSearch
{
    public static DatabaseReader CityReader;
    public static DatabaseReader ASNReader;

    public static void Init()
    {
        CityReader = new DatabaseReader($"{AppDomain.CurrentDomain.BaseDirectory}/Data/GeoLite2-City.mmdb");
        ASNReader = new DatabaseReader($"{AppDomain.CurrentDomain.BaseDirectory}/Data/GeoLite2-ASN.mmdb");
    }
}

public class GeoIpResult
{
    public string ip { get; set; } = "";

    public double? longitude { get; set; } = 0;

    public double? latitude { get; set; } = 0;
    public string? country_code { get; set; } = "";
    public string? country { get; set; } = "";
    public string? timezone { get; set; } = "";
    public long? asn { get; set; } = 0;
    public string? organization { get; set; } = "";
    public string language { get; set; } = "";
}

public class CountryCodeToLanguage
{
    private static readonly Dictionary<string, string> countryToCulture = new()
    {
        {"AF", "fa-AF"}, // Afghanistan
        {"AL", "sq-AL"}, // Albania
        {"DZ", "ar-DZ"}, // Algeria
        {"AS", "en-AS"}, // American Samoa
        {"AD", "ca-AD"}, // Andorra
        {"AO", "pt-AO"}, // Angola
        {"AI", "en-AI"}, // Anguilla
        {"AQ", "en-AQ"}, // Antarctica
        {"AG", "en-AG"}, // Antigua and Barbuda
        {"AR", "es-AR"}, // Argentina
        {"AM", "hy-AM"}, // Armenia
        {"AW", "nl-AW"}, // Aruba
        {"AU", "en-AU"}, // Australia
        {"AT", "de-AT"}, // Austria
        {"AZ", "az-AZ"}, // Azerbaijan
        {"BS", "en-BS"}, // Bahamas
        {"BH", "ar-BH"}, // Bahrain
        {"BD", "bn-BD"}, // Bangladesh
        {"BB", "en-BB"}, // Barbados
        {"BY", "be-BY"}, // Belarus
        {"BE", "nl-BE"}, // Belgium
        {"BZ", "en-BZ"}, // Belize
        {"BJ", "fr-BJ"}, // Benin
        {"BM", "en-BM"}, // Bermuda
        {"BT", "dz-BT"}, // Bhutan
        {"BO", "es-BO"}, // Bolivia
        {"BA", "bs-BA"}, // Bosnia and Herzegovina
        {"BW", "en-BW"}, // Botswana
        {"BR", "pt-BR"}, // Brazil
        {"BN", "ms-BN"}, // Brunei
        {"BG", "bg-BG"}, // Bulgaria
        {"BF", "fr-BF"}, // Burkina Faso
        {"BI", "fr-BI"}, // Burundi
        {"KH", "km-KH"}, // Cambodia
        {"CM", "fr-CM"}, // Cameroon
        {"CA", "en-CA"}, // Canada
        {"CV", "pt-CV"}, // Cape Verde
        {"KY", "en-KY"}, // Cayman Islands
        {"CF", "fr-CF"}, // Central African Republic
        {"TD", "fr-TD"}, // Chad
        {"CL", "es-CL"}, // Chile
        {"CN", "zh-CN"}, // China
        {"CO", "es-CO"}, // Colombia
        {"KM", "ar-KM"}, // Comoros
        {"CG", "fr-CG"}, // Congo
        {"CR", "es-CR"}, // Costa Rica
        {"HR", "hr-HR"}, // Croatia
        {"CU", "es-CU"}, // Cuba
        {"CY", "el-CY"}, // Cyprus
        {"CZ", "cs-CZ"}, // Czech Republic
        {"DK", "da-DK"}, // Denmark
        {"DJ", "fr-DJ"}, // Djibouti
        {"DM", "en-DM"}, // Dominica
        {"DO", "es-DO"}, // Dominican Republic
        {"EC", "es-EC"}, // Ecuador
        {"EG", "ar-EG"}, // Egypt
        {"SV", "es-SV"}, // El Salvador
        {"GQ", "es-GQ"}, // Equatorial Guinea
        {"ER", "ti-ER"}, // Eritrea
        {"EE", "et-EE"}, // Estonia
        {"ET", "am-ET"}, // Ethiopia
        {"FJ", "en-FJ"}, // Fiji
        {"FI", "fi-FI"}, // Finland
        {"FR", "fr-FR"}, // France
        {"GA", "fr-GA"}, // Gabon
        {"GM", "en-GM"}, // Gambia
        {"GE", "ka-GE"}, // Georgia
        {"DE", "de-DE"}, // Germany
        {"GH", "en-GH"}, // Ghana
        {"GR", "el-GR"}, // Greece
        {"GD", "en-GD"}, // Grenada
        {"GU", "en-GU"}, // Guam
        {"GT", "es-GT"}, // Guatemala
        {"GN", "fr-GN"}, // Guinea
        {"GW", "pt-GW"}, // Guinea-Bissau
        {"GY", "en-GY"}, // Guyana
        {"HT", "fr-HT"}, // Haiti
        {"HN", "es-HN"}, // Honduras
        {"HK", "zh-HK"}, // Hong Kong
        {"HU", "hu-HU"}, // Hungary
        {"IS", "is-IS"}, // Iceland
        {"IN", "hi-IN"}, // India
        {"ID", "id-ID"}, // Indonesia
        {"IR", "fa-IR"}, // Iran
        {"IQ", "ar-IQ"}, // Iraq
        {"IE", "en-IE"}, // Ireland
        {"IL", "he-IL"}, // Israel
        {"IT", "it-IT"}, // Italy
        {"JM", "en-JM"}, // Jamaica
        {"JP", "ja-JP"}, // Japan
        {"JO", "ar-JO"}, // Jordan
        {"KZ", "kk-KZ"}, // Kazakhstan
        {"KE", "sw-KE"}, // Kenya
        {"KI", "en-KI"}, // Kiribati
        {"KP", "ko-KP"}, // North Korea
        {"KR", "ko-KR"}, // South Korea
        {"KW", "ar-KW"}, // Kuwait
        {"KG", "ky-KG"}, // Kyrgyzstan
        {"LA", "lo-LA"}, // Laos
        {"LV", "lv-LV"}, // Latvia
        {"LB", "ar-LB"}, // Lebanon
        {"LS", "st-LS"}, // Lesotho
        {"LR", "en-LR"}, // Liberia
        {"LY", "ar-LY"}, // Libya
        {"LI", "de-LI"}, // Liechtenstein
        {"LT", "lt-LT"}, // Lithuania
        {"LU", "fr-LU"}, // Luxembourg
        {"MO", "zh-MO"}, // Macau
        {"MK", "mk-MK"}, // North Macedonia
        {"MG", "fr-MG"}, // Madagascar
        {"MW", "en-MW"}, // Malawi
        {"MY", "ms-MY"}, // Malaysia
        {"MV", "dv-MV"}, // Maldives
        {"ML", "fr-ML"}, // Mali
        {"MT", "mt-MT"}, // Malta
        {"MH", "en-MH"}, // Marshall Islands
        {"MR", "ar-MR"}, // Mauritania
        {"MU", "mfe-MU"}, // Mauritius
        {"MX", "es-MX"}, // Mexico
        {"FM", "en-FM"}, // Micronesia
        {"MD", "ro-MD"}, // Moldova
        {"MC", "fr-MC"}, // Monaco
        {"MN", "mn-MN"}, // Mongolia
        {"ME", "sr-ME"}, // Montenegro
        {"MA", "ar-MA"}, // Morocco
        {"MZ", "pt-MZ"}, // Mozambique
        {"MM", "my-MM"}, // Myanmar
        {"NA", "en-NA"}, // Namibia
        {"NR", "en-NR"}, // Nauru
        {"NP", "ne-NP"}, // Nepal
        {"NL", "nl-NL"}, // Netherlands
        {"NZ", "en-NZ"}, // New Zealand
        {"NI", "es-NI"}, // Nicaragua
        {"NE", "fr-NE"}, // Niger
        {"NG", "en-NG"}, // Nigeria
        {"NO", "no-NO"}, // Norway
        {"OM", "ar-OM"}, // Oman
        {"PK", "ur-PK"}, // Pakistan
        {"PW", "en-PW"}, // Palau
        {"PS", "ar-PS"}, // Palestine
        {"PA", "es-PA"}, // Panama
        {"PG", "en-PG"}, // Papua New Guinea
        {"PY", "es-PY"}, // Paraguay
        {"PE", "es-PE"}, // Peru
        {"PH", "en-PH"}, // Philippines
        {"PL", "pl-PL"}, // Poland
        {"PT", "pt-PT"}, // Portugal
        {"PR", "es-PR"}, // Puerto Rico
        {"QA", "ar-QA"}, // Qatar
        {"RO", "ro-RO"}, // Romania
        {"RU", "ru-RU"}, // Russia
        {"RW", "rw-RW"}, // Rwanda
        {"KN", "en-KN"}, // Saint Kitts and Nevis
        {"LC", "en-LC"}, // Saint Lucia
        {"VC", "en-VC"}, // Saint Vincent and the Grenadines
        {"WS", "en-WS"}, // Samoa
        {"SM", "it-SM"}, // San Marino
        {"ST", "pt-ST"}, // Sao Tome and Principe
        {"SA", "ar-SA"}, // Saudi Arabia
        {"SN", "fr-SN"}, // Senegal
        {"RS", "sr-RS"}, // Serbia
        {"SC", "fr-SC"}, // Seychelles
        {"SL", "en-SL"}, // Sierra Leone
        {"SG", "en-SG"}, // Singapore
        {"SK", "sk-SK"}, // Slovakia
        {"SI", "sl-SI"}, // Slovenia
        {"SB", "en-SB"}, // Solomon Islands
        {"SO", "so-SO"}, // Somalia
        {"ZA", "af-ZA"}, // South Africa
        {"SS", "en-SS"}, // South Sudan
        {"ES", "es-ES"}, // Spain
        {"LK", "si-LK"}, // Sri Lanka
        {"SD", "ar-SD"}, // Sudan
        {"SR", "nl-SR"}, // Suriname
        {"SZ", "en-SZ"}, // Swaziland
        {"SE", "sv-SE"}, // Sweden
        {"CH", "de-CH"}, // Switzerland
        {"SY", "ar-SY"}, // Syria
        {"TW", "zh-TW"}, // Taiwan
        {"TJ", "tg-TJ"}, // Tajikistan
        {"TZ", "sw-TZ"}, // Tanzania
        {"TH", "th-TH"}, // Thailand
        {"TL", "pt-TL"}, // Timor-Leste
        {"TG", "fr-TG"}, // Togo
        {"TO", "en-TO"}, // Tonga
        {"TT", "en-TT"}, // Trinidad and Tobago
        {"TN", "ar-TN"}, // Tunisia
        {"TR", "tr-TR"}, // Turkey
        {"TM", "tk-TM"}, // Turkmenistan
        {"TV", "en-TV"}, // Tuvalu
        {"UG", "en-UG"}, // Uganda
        {"UA", "uk-UA"}, // Ukraine
        {"AE", "ar-AE"}, // United Arab Emirates
        {"GB", "en-GB"}, // United Kingdom
        {"US", "en-US"}, // United States
        {"UY", "es-UY"}, // Uruguay
        {"UZ", "uz-UZ"}, // Uzbekistan
        {"VU", "en-VU"}, // Vanuatu
        {"VE", "es-VE"}, // Venezuela
        {"VN", "vi-VN"}, // Vietnam
        {"YE", "ar-YE"}, // Yemen
        {"ZM", "en-ZM"}, // Zambia
        {"ZW", "en-ZW"} // Zimbabwe
    };

    public static string GetLanguageByCountryCode(string countryCode)
    {
        if (countryToCulture.TryGetValue(countryCode, out var cultureCode))
            return cultureCode;
        return "en-US";
    }
}