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

Console.WriteLine();
Console.WriteLine(failures == 0 ? "ALLE TESTS BESTANDEN" : $"{failures} TEST(S) FEHLGESCHLAGEN");
Environment.Exit(failures == 0 ? 0 : 1);
