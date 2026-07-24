using System.Net;
using System.Net.Sockets;
using System.Text;
using DataAnonymizer.Services;

var svc = new AnonymizerService();
var opts = new AnonymizerOptions
{
    EigeneBegriffe = new List<CustomTerm> { new("Muster AG") }
};

var text = """
Schadenmeldung Muster AG

Herr Max Muster (geb. 12.03.1985), wohnhaft Bahnhofstrasse 12a, 8001 Zürich,
meldet am 15.06.2026 einen Wasserschaden. Kontakt: Tel. +41 79 123 45 67 oder
044 123 45 67, E-Mail max.muster@example.ch.

Kundin: Anna Meier-Huber, Rue de Lausanne 12, 1201 Genève.
AHV-Nr. 756.1234.5678.97, IBAN CH93 0076 2011 6238 5295 7.
Policen-Nr. P-2023/4711, Fall-Nr: 88123.
Fahrzeug ZH 456789. Kreditkarte 4539 1488 0343 6467.
Frau Meier-Huber und Herr Muster sind erreichbar.
Die Muster AG bestätigt den Eingang.
""";

var result = svc.Anonymize(text, opts);
Console.WriteLine("=== ANONYMISIERT ===");
Console.WriteLine(result.AnonymizedText);
Console.WriteLine();
Console.WriteLine("=== MAPPING ===");
foreach (var m in result.Mappings)
    Console.WriteLine($"{m.Placeholder,-16} {m.Category,-12} {m.Original}");

// --- Round-Trip-Test ---
var restored = svc.Deanonymize(result.AnonymizedText, result.Mappings);
Console.WriteLine();
Console.WriteLine("=== CHECKS ===");
var failures = 0;
void Check(string name, bool ok)
{
    Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name}");
    if (!ok) failures++;
}

var a = result.AnonymizedText;
Check("E-Mail entfernt", !a.Contains("max.muster@example.ch") && a.Contains("[EMAIL_1]"));
Check("IBAN entfernt", !a.Contains("CH93") && a.Contains("[IBAN_1]"));
Check("AHV entfernt", !a.Contains("756.1234.5678.97") && a.Contains("[AHV_1]"));
Check("Mobilnummer entfernt", !a.Contains("+41 79 123 45 67"));
Check("Festnetz entfernt", !a.Contains("044 123 45 67"));
Check("Strasse entfernt", !a.Contains("Bahnhofstrasse 12a") && a.Contains("[ADRESSE_1]"));
Check("Rue de Lausanne entfernt", !a.Contains("Rue de Lausanne 12"));
Check("PLZ/Ort entfernt", !a.Contains("8001 Zürich") && !a.Contains("1201 Genève"));
Check("Name nach Herr entfernt", !a.Contains("Max Muster"));
Check("Name nach Kundin: entfernt", !a.Contains("Anna Meier-Huber"));
Check("Gleicher Name = gleicher Platzhalter", a.Contains("Herr [NAME_1]") && a.Contains("Frau [NAME_"));
Check("Geburtsdatum entfernt", !a.Contains("12.03.1985") && a.Contains("[DATUM_1]"));
Check("Normales Datum bleibt (AlleDaten=false)", a.Contains("15.06.2026"));
Check("Policen-Nr. entfernt", !a.Contains("P-2023/4711"));
Check("Fall-Nr. entfernt", !a.Contains("88123"));
Check("Kennzeichen entfernt", !a.Contains("ZH 456789"));
Check("Kreditkarte entfernt (Luhn ok)", !a.Contains("4539 1488 0343 6467") && a.Contains("[KARTE_1]"));
Check("Eigener Begriff entfernt", !a.Contains("Muster AG") && a.Contains("[BEGRIFF_1]"));
Check("Anrede bleibt erhalten", a.Contains("Herr [NAME_1]"));
Check("Round-Trip stellt Originaltext wieder her", restored == text);

// --- Zusatztests ---
var opts2 = new AnonymizerOptions { AlleDaten = true };
var r2 = svc.Anonymize("Termin am 15.06.2026 um 14 Uhr, geboren am 01.01.1990.", opts2);
Check("AlleDaten ersetzt beide Daten", !r2.AnonymizedText.Contains("15.06.2026") && !r2.AnonymizedText.Contains("01.01.1990"));

var r3 = svc.Anonymize("Im Jahr 2024 Januar gab es 1500 Franken Umsatz.", new AnonymizerOptions());
Check("Kein falscher Ort bei '2024 Januar'", !r3.AnonymizedText.Contains("[ORT"));

var r4 = svc.Anonymize("Leerer Test ohne PII.", new AnonymizerOptions());
Check("Text ohne PII bleibt unverändert", r4.AnonymizedText == "Leerer Test ohne PII." && r4.Mappings.Count == 0);

var r5 = svc.Anonymize("", new AnonymizerOptions());
Check("Leerer Text ok", r5.AnonymizedText == "" && r5.Mappings.Count == 0);

// Luhn-negativ: zufällige 16 Ziffern, die Luhn nicht bestehen
var r6 = svc.Anonymize("Betrag 1234 5678 9012 3456 Rappen.", new AnonymizerOptions());
Check("Ungültige Kartennummer (Luhn) bleibt", r6.AnonymizedText.Contains("1234 5678 9012 3456"));

Check("'Tel' wird nicht als Name erkannt", !result.Mappings.Any(m => m.Original == "Tel"));
Check("Referenz ohne Satzpunkt", result.Mappings.Any(m => m.Original == "88123") && !result.Mappings.Any(m => m.Original == "88123."));

// --- Englisch ---
var enText = """
Mr. John Smith (born on 12/03/1985), 12 Main Street, reports a claim.
Customer: Jane Miller, phone (555) 123-4567 or 555-987-6543, SSN 078-05-1120.
Policy No. P-2023/4711, Case #88123. Dr. Brown will call back.
""";
var enOpts = new AnonymizerOptions { Language = AppLanguage.En };
var en = svc.Anonymize(enText, enOpts);
var ea = en.AnonymizedText;
Check("EN: Name nach Mr. entfernt", !ea.Contains("John Smith") && ea.Contains("Mr. [NAME_1]"));
Check("EN: Name nach Customer: entfernt", !ea.Contains("Jane Miller"));
Check("EN: Name nach Dr. entfernt", !ea.Contains("Brown"));
Check("EN: Geburtsdatum (born on) entfernt", !ea.Contains("12/03/1985"));
Check("EN: US-Telefonnummern entfernt", !ea.Contains("(555) 123-4567") && !ea.Contains("555-987-6543"));
Check("EN: US-SSN entfernt", !ea.Contains("078-05-1120") && ea.Contains("[SSN_1]"));
Check("EN: Strasse entfernt", !ea.Contains("12 Main Street") && ea.Contains("[ADDRESS_1]"));
Check("EN: Policy-Nr. entfernt", !ea.Contains("P-2023/4711"));
Check("EN: Case # entfernt", !ea.Contains("88123"));
Check("EN: Round-Trip", svc.Deanonymize(ea, en.Mappings) == enText);

// --- Französisch ---
var frText = """
Monsieur Jean Dupont, né le 12.03.1985, habite Rue de Lausanne 12, 1201 Genève.
Client : Pierre Martin. N° de police 4711. Mme Claire Dubois est informée.
""";
var frOpts = new AnonymizerOptions { Language = AppLanguage.Fr };
var fr = svc.Anonymize(frText, frOpts);
var fa = fr.AnonymizedText;
Check("FR: Name nach Monsieur entfernt", !fa.Contains("Jean Dupont") && fa.Contains("Monsieur [NOM_1]"));
Check("FR: Name nach Client : entfernt", !fa.Contains("Pierre Martin"));
Check("FR: Name nach Mme entfernt", !fa.Contains("Claire Dubois"));
Check("FR: Geburtsdatum (né le) entfernt", !fa.Contains("12.03.1985"));
Check("FR: Adresse entfernt", !fa.Contains("Rue de Lausanne 12"));
Check("FR: PLZ/Ort entfernt", !fa.Contains("1201 Genève"));
Check("FR: Policen-Nr. entfernt", !fa.Contains("4711"));
Check("FR: Round-Trip", svc.Deanonymize(fa, fr.Mappings) == frText);

// --- Italienisch ---
var itText = """
Signor Mario Rossi, nato il 12.03.1985, abita in Via Roma 8, 6900 Lugano.
Cliente: Anna Bianchi. Polizza n. 4711. La Sig.ra Bianchi è informata.
""";
var itOpts = new AnonymizerOptions { Language = AppLanguage.It };
var it = svc.Anonymize(itText, itOpts);
var ia = it.AnonymizedText;
Check("IT: Name nach Signor entfernt", !ia.Contains("Mario Rossi") && ia.Contains("Signor [NOME_1]"));
Check("IT: Name nach Cliente: entfernt", !ia.Contains("Anna Bianchi"));
Check("IT: Geburtsdatum (nato il) entfernt", !ia.Contains("12.03.1985"));
Check("IT: Adresse entfernt", !ia.Contains("Via Roma 8"));
Check("IT: PLZ/Ort entfernt", !ia.Contains("6900 Lugano"));
Check("IT: Polizza-Nr. entfernt", !ia.Contains("4711"));
Check("IT: Round-Trip", svc.Deanonymize(ia, it.Mappings) == itText);

// --- Ausgeschriebene Geburtsdaten ---
var wd = svc.Anonymize("Mrs. Smith was born on March 12, 1985. Herr Muster, geboren am 12. März 1985.", new AnonymizerOptions());
Check("Geburtsdatum 'March 12, 1985' entfernt", !wd.AnonymizedText.Contains("March 12, 1985"));
Check("Geburtsdatum '12. März 1985' entfernt", !wd.AnonymizedText.Contains("12. März 1985"));

// --- Platzhalter-Sprache ---
var labelDe = svc.Anonymize("Tel. +41 79 123 45 67", new AnonymizerOptions());
var labelEn = svc.Anonymize("Tel. +41 79 123 45 67", new AnonymizerOptions { Language = AppLanguage.En });
Check("Platzhalter Deutsch ([TELEFON_1])", labelDe.AnonymizedText.Contains("[TELEFON_1]"));
Check("Platzhalter Englisch ([PHONE_1])", labelEn.AnonymizedText.Contains("[PHONE_1]"));

// --- LLM-Funde (werden als zusätzliche Kandidaten gemischt) ---
var llmText = "Max Muster arbeitet bei der Contoso AG in Zürich. E-Mail max.muster@example.ch.";
var llmFindings = new List<LlmEntity>
{
    new("Max Muster", PiiCategory.Name),                  // Regex findet das nicht (keine Anrede)
    new("Contoso AG", PiiCategory.Organisation),
    new("max.muster@example.ch", PiiCategory.Email),      // Überschneidet mit Regex-Treffer → Regex gewinnt
    new("Nicht Im Text", PiiCategory.Name),               // Kommt nicht vor → ignorieren
    new("zürich", PiiCategory.Ort)                        // Falsche Gross-/Kleinschreibung → trotzdem finden
};
var llm = svc.Anonymize(llmText, new AnonymizerOptions(), llmFindings);
var la = llm.AnonymizedText;
Check("LLM: Name ohne Anrede entfernt", !la.Contains("Max Muster") && la.Contains("[NAME_1]"));
Check("LLM: Firma entfernt", !la.Contains("Contoso AG") && la.Contains("[FIRMA_1]"));
Check("LLM: E-Mail nur einmal ersetzt", la.Contains("[EMAIL_1]") && llm.Mappings.Count(m => m.Category == PiiCategory.Email) == 1);
Check("LLM: Unbekannter Fund ignoriert", !llm.Mappings.Any(m => m.Original == "Nicht Im Text"));
Check("LLM: Fund trotz anderer Schreibweise", !la.Contains("Zürich"));
Check("LLM: Round-Trip", svc.Deanonymize(la, llm.Mappings) == llmText);

var llmOff = svc.Anonymize(llmText, new AnonymizerOptions { Namen = false, Organisationen = false }, llmFindings);
Check("LLM: Deaktivierte Kategorien werden übersprungen",
    llmOff.AnonymizedText.Contains("Max Muster") && llmOff.AnonymizedText.Contains("Contoso AG"));

// --- Parsen der LLM-Antwort ---
var parsed = LocalLlmClient.ParseEntities(
    """{"entities":[{"text":"Max Muster","category":"name"},{"text":"Contoso AG","category":"organization"},{"text":"Max Muster","category":"name"}]}""");
Check("Parse: Entitäten gelesen und dedupliziert", parsed.Count == 2
    && parsed[0] == new LlmEntity("Max Muster", PiiCategory.Name)
    && parsed[1] == new LlmEntity("Contoso AG", PiiCategory.Organisation));

var parsedFenced = LocalLlmClient.ParseEntities(
    "Here is the result:\n```json\n{\"entities\":[{\"text\":\"Anna\",\"category\":\"person\"}]}\n```");
Check("Parse: JSON in Code-Fence", parsedFenced.Count == 1 && parsedFenced[0].Category == PiiCategory.Name);

Check("Parse: Unbekannte Kategorie wird Begriff",
    LocalLlmClient.ParseEntities("""{"entities":[{"text":"X1","category":"whatever"}]}""").Single().Category == PiiCategory.Begriff);
Check("Parse: Müll ergibt leere Liste", LocalLlmClient.ParseEntities("no json here").Count == 0);
Check("Parse: Kaputtes JSON ergibt leere Liste", LocalLlmClient.ParseEntities("{\"entities\":[{").Count == 0);

// --- Fortschritts-Zeilen des Modell-Downloads (/api/pull) ---
var pullLine = LocalLlmClient.ParsePullLine("""{"status":"downloading","total":1000,"completed":250}""");
Check("Pull: Prozent berechnet", pullLine is { Percent: 25, IsSuccess: false, IsError: false });
Check("Pull: success erkannt", LocalLlmClient.ParsePullLine("""{"status":"success"}""") is { IsSuccess: true });
Check("Pull: Fehler erkannt", LocalLlmClient.ParsePullLine("""{"error":"model not found"}""") is { IsError: true });
Check("Pull: Müll ergibt null", LocalLlmClient.ParsePullLine("not json") is null);

// --- LocalLlmClient gegen einen Fake-Ollama-Server ---
static int GetFreePort()
{
    var probe = new TcpListener(IPAddress.Loopback, 0);
    probe.Start();
    var port = ((IPEndPoint)probe.LocalEndpoint).Port;
    probe.Stop();
    return port;
}

var fakePort = GetFreePort();
var listener = new HttpListener();
listener.Prefixes.Add($"http://127.0.0.1:{fakePort}/");
listener.Start();
var serverTask = Task.Run(async () =>
{
    while (listener.IsListening)
    {
        HttpListenerContext ctx;
        try { ctx = await listener.GetContextAsync(); }
        catch { break; }

        var body = ctx.Request.Url!.AbsolutePath switch
        {
            "/api/tags" => """{"models":[{"name":"llama3.2:latest"},{"name":"nomic-embed-text:latest"}]}""",
            "/api/chat" => """{"message":{"role":"assistant","content":"{\"entities\":[{\"text\":\"Max Muster\",\"category\":\"name\"}]}"}}""",
            _ => "{}"
        };
        var bytes = Encoding.UTF8.GetBytes(body);
        ctx.Response.ContentType = "application/json";
        ctx.Response.ContentLength64 = bytes.Length;
        await ctx.Response.OutputStream.WriteAsync(bytes);
        ctx.Response.Close();
    }
});

using (var fakeClient = new LocalLlmClient(new LocalLlmOptions { Endpoint = $"http://127.0.0.1:{fakePort}" }))
{
    var state = await fakeClient.GetStateAsync();
    Check("Ollama-State: erreichbar inkl. Modellliste", state.Reachable && state.Models.Contains("llama3.2:latest"));

    var detected = await fakeClient.DetectPiiAsync("Max Muster wohnt hier.", "llama3.2:latest");
    Check("DetectPii über HTTP", detected.Count == 1 && detected[0] == new LlmEntity("Max Muster", PiiCategory.Name));
}
listener.Stop();
await serverTask;

using (var offlineClient = new LocalLlmClient(new LocalLlmOptions { Endpoint = $"http://127.0.0.1:{GetFreePort()}" }))
{
    var offlineState = await offlineClient.GetStateAsync();
    Check("Ollama-State: offline erkannt", offlineState is { Reachable: false, Models.Count: 0 });
}

Console.WriteLine();
Console.WriteLine(failures == 0 ? "ALLE TESTS BESTANDEN" : $"{failures} TEST(S) FEHLGESCHLAGEN");
Environment.Exit(failures == 0 ? 0 : 1);
