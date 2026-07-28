namespace Website_API.Data;

public static class GeorgianLocationTranslations
{
    private static readonly Dictionary<string, string> Cities =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Tbilisi"] = "თბილისი",
            ["Batumi"] = "ბათუმი",
            ["Kutaisi"] = "ქუთაისი",
            ["Rustavi"] = "რუსთავი",
            ["Poti"] = "ფოთი",
            ["Zugdidi"] = "ზუგდიდი",
            ["Telavi"] = "თელავი",
            ["Gori"] = "გორი",
            ["Mcxeta"] = "მცხეთა",
            ["Mtskheta"] = "მცხეთა",
            ["Georgia"] = "საქართველო"
        };

    private static readonly Dictionary<string, string> Regions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Vake-Saburtalo"] = "ვაკე-საბურთალო",
            ["Isani-Samgori"] = "ისანი-სამგორი",
            ["Gldani-Nadzaladevi"] = "გლდანი-ნაძალადევი",
            ["Didube-Chughureti"] = "დიდუბე-ჩუღურეთი",
            ["Old Tbilisi"] = "ძველი თბილისი",
            ["Districts of Batumi"] = "ბათუმის უბნები",
            ["Districts of Kutaisi"] = "ქუთაისის უბნები",
            ["Districts of Rustavi"] = "რუსთავის უბნები",
            ["Other Regions"] = "სხვა რეგიონები",
            ["Suburbs Of Tbilisi"] = "თბილისის შემოგარენი",
            ["Municipalities"] = "მუნიციპალიტეტები"
        };

    private static readonly Dictionary<string, string> Districts =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Saburtalo"] = "საბურთალო",
            ["Vake"] = "ვაკე",
            ["Didi Digomi"] = "დიდი დიღომი",
            ["Digomi"] = "დიღომი",
            ["Digomi Village"] = "სოფელი დიღომი",
            ["Bagebi"] = "ბაგები",
            ["Lisi Lake"] = "ლისის ტბა",
            ["Turtle Lake"] = "კუს ტბა",
            ["Vashlijvari"] = "ვაშლიჯვარი",
            ["Vedzisi"] = "ვეძისი",
            ["Tkhinvala"] = "თხინვალა",
            ["Vazisubani"] = "ვაზისუბანი",
            ["Varketili"] = "ვარკეთილი",
            ["Isani"] = "ისანი",
            ["Lilo"] = "ლილო",
            ["Ortachala"] = "ორთაჭალა",
            ["Orkhevi"] = "ორხევი",
            ["Samgori"] = "სამგორი",
            ["Ponichala"] = "ფონიჭალა",
            ["Avchala"] = "ავჭალა",
            ["Gldani"] = "გლდანი",
            ["Zahesi"] = "ზაჰესი",
            ["Temqa"] = "თემქა",
            ["Nadzaladevi"] = "ნაძალადევი",
            ["Sanzona"] = "სანზონა",
            ["Didube"] = "დიდუბე",
            ["Kukia"] = "კუკია",
            ["Chugureti"] = "ჩუღურეთი",
            ["Abanotubani"] = "აბანოთუბანი",
            ["Avlabari"] = "ავლაბარი",
            ["Elia"] = "ელია",
            ["Vera"] = "ვერა",
            ["Mtatsminda"] = "მთაწმინდა",
            ["Sololaki"] = "სოლოლაკი",
            ["Makhinjauri"] = "მახინჯაური",
            ["Balakhvani"] = "ბალახვანი",
            ["Vakisubani"] = "ვაკისუბანი",
            ["Safichkhia"] = "საფიჩხია",
            ["Ukimerioni"] = "უქიმერიონი",
            ["New Rustavi"] = "ახალი რუსთავი",
            ["Old Rustavi"] = "ძველი რუსთავი",
            ["Poti"] = "ფოთი",
            ["Zugdidi"] = "ზუგდიდი",
            ["Telavi"] = "თელავი",
            ["Gori"] = "გორი",
            ["Mcxeta"] = "მცხეთა",
            ["Agaraki"] = "აგარაკი",
            ["Akhaldaba"] = "ახალდაბა",
            ["Betania"] = "ბეთანია",
            ["Didgori"] = "დიდგორი",
            ["Didi Lilo"] = "დიდი ლილო",
            ["Kiketi"] = "კიკეთი",
            ["Kojori"] = "კოჯორი",
            ["Okrokana"] = "ოქროყანა",
            ["Shindisi"] = "შინდისი",
            ["Tabakhmela"] = "ტაბახმელა",
            ["Tsavkisi"] = "წავკისი",
            ["Tskneti"] = "წყნეთი"
        };

    public static string? FindCity(string english) =>
        Find(Cities, english) ??
        VerifiedGeorgianLocations.Find(english);

    public static string? FindRegion(string english) =>
        Find(Regions, english);

    public static string? FindDistrict(string english) =>
        Find(Districts, english) ??
        VerifiedGeorgianLocations.Find(english);

    public static string? FindEnglishCity(string georgian) =>
        FindEnglish(Cities, georgian) ??
        VerifiedGeorgianLocations.FindEnglish(georgian);

    public static string? FindEnglishRegion(string georgian) =>
        FindEnglish(Regions, georgian);

    public static string? FindEnglishDistrict(string georgian) =>
        FindEnglish(Districts, georgian) ??
        VerifiedGeorgianLocations.FindEnglish(georgian);

    private static string? Find(
        IReadOnlyDictionary<string, string> values,
        string english) =>
        values.TryGetValue(english.Trim(), out var georgian)
            ? georgian
            : null;

    private static string? FindEnglish(
        IReadOnlyDictionary<string, string> values,
        string georgian) =>
        values.FirstOrDefault(item =>
            item.Value.Equals(
                georgian.Trim(),
                StringComparison.OrdinalIgnoreCase)).Key;
}
