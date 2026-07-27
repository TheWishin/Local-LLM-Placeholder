using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using DataAnonymizer.Proxy;
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

// --- Erlaubte Werte (Allowlist) ---
var allowOpts = new AnonymizerOptions { ErlaubteWerte = new List<string> { "8001  zürich", "Max Muster" } };
var allow = svc.Anonymize("Herr Max Muster wohnt in 8001 Zürich, Frau Anna Meier auch. Tel. +41 79 123 45 67.", allowOpts);
Check("Allow: Erlaubter Name bleibt sichtbar", allow.AnonymizedText.Contains("Max Muster"));
Check("Allow: Erlaubter Ort bleibt (case-insensitiv, Leerraum egal)", allow.AnonymizedText.Contains("8001 Zürich"));
Check("Allow: Andere Namen werden weiter ersetzt", !allow.AnonymizedText.Contains("Anna Meier"));
Check("Allow: Andere Kategorien unberührt", !allow.AnonymizedText.Contains("+41 79 123 45 67"));

var allowLlm = svc.Anonymize("Max Muster arbeitet bei der Contoso AG.",
    new AnonymizerOptions { ErlaubteWerte = new List<string> { "Contoso AG" } },
    new List<LlmEntity> { new("Max Muster", PiiCategory.Name), new("Contoso AG", PiiCategory.Organisation) });
Check("Allow: Gilt auch für LLM-Funde", allowLlm.AnonymizedText.Contains("Contoso AG") && !allowLlm.AnonymizedText.Contains("Max Muster"));

// --- Swiss-DSG-Detektoren (IP, UID, Versichertenkarte, sensible Daten) ---
var dsgText = "Server 192.168.10.14 und 2001:db8::ff00:42:8329. Firma CHE-123.456.789 MWST. Karte 80756009012345678901.";
var dsg = svc.Anonymize(dsgText, new AnonymizerOptions());
var da = dsg.AnonymizedText;
Check("DSG: IPv4 entfernt", !da.Contains("192.168.10.14") && da.Contains("[IP_1]"));
Check("DSG: IPv6 entfernt", !da.Contains("2001:db8::ff00:42:8329"));
Check("DSG: UID (CHE) entfernt", !da.Contains("CHE-123.456.789"));
Check("DSG: Versichertenkarte entfernt", !da.Contains("80756009012345678901"));
Check("DSG: Round-Trip", svc.Deanonymize(da, dsg.Mappings) == dsgText);

var ipOff = svc.Anonymize("Server 192.168.10.14 down.", new AnonymizerOptions { IpAdressen = false });
Check("DSG: IP-Erkennung abschaltbar", ipOff.AnonymizedText.Contains("192.168.10.14"));

// Sensible Daten kommen vom lokalen LLM.
var sens = svc.Anonymize("Der Kunde leidet an Depression und ist Mitglied der Gewerkschaft Unia.",
    new AnonymizerOptions(),
    new List<LlmEntity> { new("Depression", PiiCategory.Sensitiv), new("Mitglied der Gewerkschaft Unia", PiiCategory.Sensitiv) });
Check("DSG: sensible Daten (LLM) entfernt",
    !sens.AnonymizedText.Contains("Depression") && sens.AnonymizedText.Contains("[SENSIBEL_1]"));
Check("DSG: sensible Kategorie abschaltbar",
    svc.Anonymize("leidet an Depression", new AnonymizerOptions { SensitiveDaten = false },
        new List<LlmEntity> { new("Depression", PiiCategory.Sensitiv) }).AnonymizedText.Contains("Depression"));

Check("DSG: Kategorie-IDs Rundlauf (ip/sensitive)",
    PiiCategoryIds.FromId("ip") == PiiCategory.Ip && PiiCategoryIds.FromId("sensitive") == PiiCategory.Sensitiv
    && PiiCategoryIds.ToId(PiiCategory.Ip) == "ip" && PiiCategoryIds.ToId(PiiCategory.Sensitiv) == "sensitive");

// LLM-Kategorien für sensible Daten korrekt zugeordnet.
Check("DSG: LLM-Kategorie 'health' -> sensitiv",
    LocalLlmClient.ParseEntities("""{"entities":[{"text":"Diabetes","category":"health"}]}""").Single().Category == PiiCategory.Sensitiv);
Check("DSG: LLM-Kategorie 'ip' -> Ip",
    LocalLlmClient.ParseEntities("""{"entities":[{"text":"10.0.0.5","category":"ip"}]}""").Single().Category == PiiCategory.Ip);

// --- Neue Detektoren: VIN, BIC/SWIFT, USt-IdNr/VAT ---
var vinText = "Fahrzeug FIN 1HGBH41JXMN109186 im Vertrag.";
var vin = svc.Anonymize(vinText, new AnonymizerOptions());
Check("VIN erkannt und ersetzt", !vin.AnonymizedText.Contains("1HGBH41JXMN109186") && vin.AnonymizedText.Contains("[REFERENZ_1]"));
Check("VIN: als Referenz-Kategorie", vin.Mappings.Any(m => m.Category == PiiCategory.Referenz && m.Original == "1HGBH41JXMN109186"));
Check("VIN: 16 Zeichen sind keine VIN", svc.Anonymize("Kurz 1HGBH41JXMN10918 Ende.", new AnonymizerOptions()).AnonymizedText.Contains("1HGBH41JXMN10918"));
Check("VIN: Round-Trip", svc.Deanonymize(vin.AnonymizedText, vin.Mappings) == vinText);

var bicText = "Bank BIC: DEUTDEFF500 und SWIFT-Code: UBSWCHZH80A.";
var bic = svc.Anonymize(bicText, new AnonymizerOptions());
Check("BIC (11) erkannt", !bic.AnonymizedText.Contains("DEUTDEFF500") && bic.AnonymizedText.Contains("[IBAN_1]"));
Check("SWIFT (11) erkannt", !bic.AnonymizedText.Contains("UBSWCHZH80A"));
Check("BIC: als IBAN-Kategorie", bic.Mappings.Any(m => m.Category == PiiCategory.Iban && m.Original == "DEUTDEFF500"));
Check("BIC: kurzer BIC (8) erkannt", !svc.Anonymize("BIC DEUTDEFF hier.", new AnonymizerOptions()).AnonymizedText.Contains("DEUTDEFF"));
Check("BIC: 8-Buchstaben-Wort ohne Schlüsselwort ist kein BIC", svc.Anonymize("Der Code DEUTDEFF ohne Kontext.", new AnonymizerOptions()).AnonymizedText.Contains("DEUTDEFF"));
Check("BIC: Round-Trip", svc.Deanonymize(bic.AnonymizedText, bic.Mappings) == bicText);

var vatText = "USt-IdNr.: DE123456789, VAT: ATU12345678, N. IVA: IT12345678901, TVA: FR12345678901.";
var vat = svc.Anonymize(vatText, new AnonymizerOptions());
Check("VAT DE erkannt", !vat.AnonymizedText.Contains("DE123456789"));
Check("VAT AT erkannt", !vat.AnonymizedText.Contains("ATU12345678"));
Check("VAT IT erkannt", !vat.AnonymizedText.Contains("IT12345678901"));
Check("VAT FR erkannt", !vat.AnonymizedText.Contains("FR12345678901"));
Check("VAT: als Referenz-Kategorie", vat.Mappings.Any(m => m.Category == PiiCategory.Referenz && m.Original == "DE123456789"));
Check("VAT: Nummer ohne Schlüsselwort bleibt", svc.Anonymize("Wert DE123456789 im Text.", new AnonymizerOptions()).AnonymizedText.Contains("DE123456789"));
Check("VAT: Round-Trip", svc.Deanonymize(vat.AnonymizedText, vat.Mappings) == vatText);

// --- SQL-Skript-Rundlauf: Platzhalter tolerant zurückersetzen ---
var sqlMappings = new List<MappingEntry>
{
    new("[NAME_1]", "Max Muster", PiiCategory.Name),
    new("[EMAIL_1]", "max.muster@example.ch", PiiCategory.Email),
    new("[REFERENZ_1]", "P-2023/4711", PiiCategory.Referenz),
    new("[NAME_11]", "Anna Andere", PiiCategory.Name)
};
var sqlScript = """
INSERT INTO customers (name, email, policy_no)
VALUES ('[NAME_1]', '[ email_1 ]', '[Referenz_1]');
UPDATE customers SET name = '[NAME_11]' WHERE email = '[EMAIL_1]';
""";
var sqlRestored = svc.Deanonymize(sqlScript, sqlMappings);
Check("SQL: exakter Platzhalter ersetzt", sqlRestored.Contains("'Max Muster'"));
Check("SQL: Platzhalter mit Leerzeichen ersetzt", sqlRestored.Contains("'max.muster@example.ch'"));
Check("SQL: Platzhalter mit anderer Schreibweise ersetzt", sqlRestored.Contains("'P-2023/4711'"));
Check("SQL: [NAME_11] wird nicht von [NAME_1] getroffen", sqlRestored.Contains("'Anna Andere'"));
Check("SQL: keine Platzhalter übrig", !sqlRestored.Contains("[NAME_") && !sqlRestored.Contains("[ email_1 ]"));
Check("Deanonymize: $ im Originalwert bleibt wörtlich",
    svc.Deanonymize("x [NAME_1] y", new[] { new MappingEntry("[NAME_1]", "A$&B$1", PiiCategory.Name) }) == "x A$&B$1 y");

// --- Kategorie-IDs für Export/Import (kompatibel mit der Erweiterung) ---
Check("Kategorie-IDs: Rundlauf über alle Kategorien",
    Enum.GetValues<PiiCategory>().All(c => PiiCategoryIds.FromId(PiiCategoryIds.ToId(c)) == c));
Check("Kategorie-IDs: Unbekannt wird Begriff", PiiCategoryIds.FromId("whatever") == PiiCategory.Begriff);

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

// =====================================================================
// API-Gateway (DataAnonymizer.Proxy): Anonymisierungs-Round-Trip
// =====================================================================
Console.WriteLine();
Console.WriteLine("=== API-GATEWAY ===");

// AnonymizeMany: gemeinsame Zuordnung über mehrere Texte hinweg.
var manyOpts = new AnonymizerOptions { Language = AppLanguage.En };
var many = svc.AnonymizeMany(
    new[] { "Mail: max.muster@example.ch", "Nochmals max.muster@example.ch und CH93 0076 2011 6238 5295 7" },
    manyOpts);
Check("AnonymizeMany: gleicher Wert → gleicher Platzhalter über Texte",
    many.AnonymizedTexts[0].Contains("[EMAIL_1]") && many.AnonymizedTexts[1].Contains("[EMAIL_1]"));
Check("AnonymizeMany: zweiter Text hat eigene IBAN-Kategorie",
    many.AnonymizedTexts[1].Contains("[IBAN_1]") && !many.AnonymizedTexts[1].Contains("CH93"));
Check("AnonymizeMany: Mapping enthält genau E-Mail + IBAN", many.Mappings.Count == 2);

// Anfrage-Body umschreiben: System + zwei Nachrichten (String + Block-Liste), stream bleibt.
var reqJson = """
{"model":"claude-3-5-sonnet","max_tokens":200,"stream":true,
 "system":"Bitte hilf. Mail: max.muster@example.ch",
 "messages":[
   {"role":"user","content":"Nochmals: max.muster@example.ch"},
   {"role":"user","content":[{"type":"text","text":"IBAN CH93 0076 2011 6238 5295 7"},{"type":"image","source":{"type":"base64","media_type":"image/png","data":"AAAA"}}]}
 ]}
""";
var rew = AnthropicRewriter.AnonymizeRequestBody(reqJson, svc, new AnonymizerOptions { Language = AppLanguage.En });
Check("Gateway-Request: E-Mail ersetzt", !rew.Json.Contains("max.muster@example.ch") && rew.Json.Contains("[EMAIL_1]"));
Check("Gateway-Request: IBAN ersetzt", !rew.Json.Contains("CH93 0076 2011 6238 5295 7") && rew.Json.Contains("[IBAN_1]"));
Check("Gateway-Request: stream-Flag bleibt erhalten", rew.Json.Contains("\"stream\":true") || rew.Json.Contains("\"stream\": true"));
Check("Gateway-Request: Bild-Block bleibt unangetastet", rew.Json.Contains("image/png") && rew.Json.Contains("\"data\":\"AAAA\""));
Check("Gateway-Request: Mapping = E-Mail + IBAN", rew.Mappings.Count == 2);
// Body bleibt gültiges JSON.
var reparse = false;
try { JsonNode.Parse(rew.Json); reparse = true; } catch { }
Check("Gateway-Request: Ergebnis ist gültiges JSON", reparse);

// Antwort (nicht gestreamt) zurückübersetzen – inkl. Platzhalter im SQL-Tool-Argument.
var respJson = """
{"id":"msg_1","type":"message","role":"assistant","content":[
  {"type":"text","text":"Ich melde mich bei [EMAIL_1]."},
  {"type":"tool_use","id":"t1","name":"run_sql","input":{"query":"SELECT * FROM kunden WHERE iban = '[IBAN_1]'"}}
]}
""";
var restoredResp = AnthropicRewriter.DeanonymizeResponseBody(respJson, rew.Mappings, svc);
Check("Gateway-Response: E-Mail wiederhergestellt", restoredResp.Contains("max.muster@example.ch") && !restoredResp.Contains("[EMAIL_1]"));
Check("Gateway-Response: IBAN im SQL wiederhergestellt", restoredResp.Contains("CH93 0076 2011 6238 5295 7") && !restoredResp.Contains("[IBAN_1]"));
var respParse = false;
try { JsonNode.Parse(restoredResp); respParse = true; } catch { }
Check("Gateway-Response: bleibt gültiges JSON", respParse);

// Streaming (SSE): Platzhalter über zwei Chunks verteilt wird korrekt zusammengesetzt.
var nameMappings = new List<MappingEntry> { new("[NAME_1]", "Max Muster", PiiCategory.Name) };
var sse = new SseDeanonymizer(nameMappings, svc);
var sseOut = new StringBuilder();
sseOut.Append(sse.Push(
    "event: content_block_start\n" +
    "data: {\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"text\",\"text\":\"\"}}\n\n" +
    "event: content_block_delta\n" +
    "data: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\"Hallo [NA\"}}\n\n"));
sseOut.Append(sse.Push(
    "event: content_block_delta\n" +
    "data: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\"ME_1]!\"}}\n\n" +
    "event: content_block_stop\n" +
    "data: {\"type\":\"content_block_stop\",\"index\":0}\n\n"));
sseOut.Append(sse.Complete());
var sseText = sseOut.ToString();
Check("Gateway-Stream: geteilter Platzhalter wird zusammengesetzt", sseText.Contains("Max Muster"));
Check("Gateway-Stream: kein Platzhalter-Rest im Strom", !sseText.Contains("[NAME_1]") && !sseText.Contains("[NA"));
Check("Gateway-Stream: Stop-Ereignis bleibt erhalten", sseText.Contains("content_block_stop"));

// Streaming eines Tool-Arguments (input_json_delta): Platzhalter im JSON-Fragment.
var ibanMappings = new List<MappingEntry> { new("[IBAN_1]", "CH93 0076 2011 6238 5295 7", PiiCategory.Iban) };
var sse2 = new SseDeanonymizer(ibanMappings, svc);
var delta2 = new JsonObject { ["type"] = "input_json_delta", ["partial_json"] = "{\"q\":\"[IBAN_1]\"}" };
var evt2 = new JsonObject { ["type"] = "content_block_delta", ["index"] = 0, ["delta"] = delta2 };
var sse2Out = sse2.Push("event: content_block_delta\ndata: " + evt2.ToJsonString() + "\n\n")
    + sse2.Push("event: content_block_stop\ndata: {\"type\":\"content_block_stop\",\"index\":0}\n\n")
    + sse2.Complete();
Check("Gateway-Stream: Platzhalter im Tool-JSON wiederhergestellt", sse2Out.Contains("CH93 0076 2011 6238 5295 7") && !sse2Out.Contains("[IBAN_1]"));
var sse2ParseOk = true;
foreach (var l in sse2Out.Split('\n'))
{
    if (l.StartsWith("data: {"))
    {
        try { JsonNode.Parse(l["data: ".Length..]); } catch { sse2ParseOk = false; }
    }
}
Check("Gateway-Stream: Datenzeilen bleiben gültiges JSON", sse2ParseOk);

// JSON-Escaping für Streaming-Fragmente (Wert mit Anführungszeichen).
Check("JsonEscapeInner escaped Anführungszeichen", AnthropicRewriter.JsonEscapeInner("a\"b") == "a\\\"b");

// Sprache konfigurierbar.
Check("ProxyOptions: Sprache 'de' → De", ProxyOptions.ParseLanguage("de") == AppLanguage.De);
Check("ProxyOptions: Sprache leer → En", ProxyOptions.ParseLanguage(null) == AppLanguage.En);

Console.WriteLine();
Console.WriteLine(failures == 0 ? "ALLE TESTS BESTANDEN" : $"{failures} TEST(S) FEHLGESCHLAGEN");
Environment.Exit(failures == 0 ? 0 : 1);
 
