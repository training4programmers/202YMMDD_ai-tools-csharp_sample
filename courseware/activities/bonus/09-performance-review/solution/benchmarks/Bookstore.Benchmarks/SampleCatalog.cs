using Bookstore.Domain;

namespace Bookstore.Benchmarks;

/// <summary>
/// A made-up catalog of any size, the same every time for the same seed. The benchmarks and the
/// equivalence tests (which link this file) both use it.
/// </summary>
public static class SampleCatalog
{
    private static readonly string[] TitleWords =
    [
        "The", "Quiet", "Compiler", "Rivers", "of", "Glass", "Patterns", "in", "the", "Noise", "Async", "All",
        "Way", "Down", "Lighthouse", "Cat", "Maps", "We", "Never", "Drew", "Northern", "Lights", "Field",
        "Guide", "Bridges", "History", "Short", "A", "Garden", "Winter", "Letters", "Silent", "Engine",
        "Harbor", "Clock", "Paper", "Moon", "Stone", "Song", "Last", "First", "Little", "Brave", "Hidden",
    ];

    private static readonly string[] FirstNames =
    [
        "Maya", "Lars", "Priya", "Tomás", "Ana", "Kenji", "Fatima", "Oliver", "Ines", "Samuel", "Leila",
        "Marco", "Yuki", "Grace", "Omar", "Helena",
    ];

    private static readonly string[] LastNames =
    [
        "Okafor", "Henriksen", "Raman", "Ortega", "Silva", "Tanaka", "Haddad", "Brooks", "Moreau", "Mensah",
        "Novak", "Rossi", "Kowalski", "Lindqvist", "Adeyemi", "Fischer",
    ];

    public static Book[] Generate(int count, int seed)
    {
        var random = new Random(seed);
        var authors = Enumerable.Range(1, 200)
            .Select(id => new Author
            {
                Id = id,
                Name = $"{FirstNames[random.Next(FirstNames.Length)]} {LastNames[random.Next(LastNames.Length)]}",
            })
            .ToArray();

        return
        [
            .. Enumerable.Range(1, count).Select(id =>
            {
                var author = authors[random.Next(authors.Length)];
                var title = string.Join(' ', Enumerable.Range(0, random.Next(2, 6)).Select(_ => TitleWords[random.Next(TitleWords.Length)]));
                return new Book { Id = id, Title = title, Isbn = "978-1-23456-001-0", Author = author, AuthorId = author.Id };
            }),
        ];
    }
}
