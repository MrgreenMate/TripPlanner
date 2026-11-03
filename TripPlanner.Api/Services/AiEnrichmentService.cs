using System.Text;
using System.Text.Json;
using TripPlanner.Api.Models;

namespace TripPlanner.Api.Services
{
    public class AiEnrichmentService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey = "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";

        public AiEnrichmentService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<(Poi Poi, List<Review> Reviews)>> EnrichPoisWithReviewsAsync(List<RawPoi> rawPois)
        {
            if (!rawPois.Any()) return new List<(Poi, List<Review>)>();

            var namesList = string.Join(", ", rawPois.Select(p => p.Name));

          
            var prompt = $@"
                Turisztikai szakértő vagy. Itt egy lista látványosságokról: {namesList}.
                Értékeld mindegyiket 1-től 5-ig (priority) turisztikai fontosság alapján, és adj hozzájuk egy 1 mondatos magyar nyelvű leírást.
                FIGYELEM: Ha egy név alapján az adott hely egy hétköznapi cég (pl. Kft., gyár, hulladékfeldolgozó), kisállatpanzió, bolt, vagy egyáltalán nem turisztikai célpont, akkor a priority értéke kötelezően 0 legyen!
                Továbbá generálj a valódi turisztikai helyekhez 5-10 darab változatos, valósághű magyar nyelvű turistavéleményt (3-5 csillagos ratinggel)!
                A válaszod SZIGORÚAN egy JSON tömb legyen a következő formátumban:
                [
                  {{ 
                    ""name"": ""Név"", 
                    ""priority"": 4, 
                    ""description"": ""Rövid leírás"",
                    ""reviews"": [
                        {{ ""rating"": 5, ""comment"": ""Gyönyörű hely!"" }}
                    ]
                  }}
                ]
                Ne írj semmi mást a JSON-ön kívül!";

            var requestBody = new
            {
                model = "gpt-5.4-nano", 
                messages = new[] { new { role = "user", content = prompt } },
                temperature = 0.7
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
            request.Headers.Add("Authorization", $"Bearer {_apiKey}");
            request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            var resultList = new List<(Poi Poi, List<Review> Reviews)>();

            // FALLBACK
            if (!response.IsSuccessStatusCode)
            {
                var random = new Random();
                Console.WriteLine("FALLBACK");
                var fallbackComments = new[] {
                    "Nagyon tetszett, lenyűgöző volt!",
                    "Kellemes hely, de nagy volt a tömeg.",
                    "Megérte megnézni, szép élmény.",
                    "Szuper program volt a családnak.",
                    "Gyönyörű, biztosan visszatérünk még!",
                    "Kicsit drága a környék, de a látvány kárpótolt.",
                    "Könnyen megközelíthető, szép környezet."
                };

                foreach (var raw in rawPois)
                {
                   
                    var fallbackPoi = new Poi
                    {
                        Name = raw.Name,
                        Latitude = raw.Latitude,
                        Longitude = raw.Longitude,
                        Priority = random.Next(3, 6),
                        Description = "Automatikusan importált hely (AI nélkül)."
                    };

                    var fallbackReviews = new List<Review>();
                    int reviewCount = random.Next(3, 6); 

                    for (int i = 0; i < reviewCount; i++)
                    {
                        fallbackReviews.Add(new Review
                        {
                            Rating = random.Next(3, 6),
                            Comment = fallbackComments[random.Next(fallbackComments.Length)],
            
                            CreatedUtc = DateTime.UtcNow.AddDays(-random.Next(1, 60)),
                            UserId = null
                        });
                    }
                    resultList.Add((fallbackPoi, fallbackReviews));
                }
                return resultList;
            }

          
            var jsonResponse = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonResponse);
            var aiText = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

            var enrichedData = JsonSerializer.Deserialize<List<EnrichedPoiDto>>(aiText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            foreach (var raw in rawPois)
            {
                var aiData = enrichedData?.FirstOrDefault(e => e.Name.ToLower() == raw.Name.ToLower());

                var poi = new Poi
                {
                    Name = raw.Name,
                    Latitude = raw.Latitude,
                    Longitude = raw.Longitude,
                    Priority = aiData?.Priority ?? 3,
                    Description = aiData?.Description ?? "Automatikusan importált hely."
                };

                var reviews = new List<Review>();
                if (aiData != null && aiData.Reviews != null)
                {
                    foreach (var rev in aiData.Reviews)
                    {
                        reviews.Add(new Review
                        {
                            Rating = rev.Rating,
                            Comment = rev.Comment,
                            CreatedUtc = DateTime.UtcNow.AddDays(-new Random().Next(1, 30)),
                            UserId = null
                        });
                    }
                }

                resultList.Add((poi, reviews));
            }

            return resultList;
        }

        private class EnrichedPoiDto
        {
            public string Name { get; set; } = string.Empty;
            public int Priority { get; set; }
            public string Description { get; set; } = string.Empty;
            public List<EnrichedReviewDto> Reviews { get; set; } = new();
        }

        private class EnrichedReviewDto
        {
            public int Rating { get; set; }
            public string Comment { get; set; } = string.Empty;
        }
    }
}