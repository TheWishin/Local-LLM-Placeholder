using DataAnonymizer.Services;

namespace DataAnonymizer;

/// <summary>Alle Oberflächen-Texte einer Sprache.</summary>
public sealed class UiText
{
    public required string PageTitle { get; init; }
    public required string AppTitle { get; init; }
    public required string Tagline { get; init; }

    public required string CardOriginal { get; init; }
    public required string InputPlaceholder { get; init; }
    public required string BtnAnonymize { get; init; }
    public required string BtnAnonymizeBusy { get; init; }
    public required string BtnClear { get; init; }

    public required string CardAnonymized { get; init; }
    public required string BtnCopy { get; init; }
    public required string Copied { get; init; }
    public required string MappingTitle { get; init; }        // {0} = Anzahl
    public required string ThPlaceholder { get; init; }
    public required string ThOriginal { get; init; }
    public required string ThCategory { get; init; }
    public required string MappingNote { get; init; }
    public required string NoPiiFound { get; init; }

    public required string CardDeanonymize { get; init; }
    public required string DeanonHint { get; init; }
    public required string ResponsePlaceholder { get; init; }
    public required string BtnDeanonymize { get; init; }
    public required string RestoredTitle { get; init; }

    public required string CardRules { get; init; }
    public required string OptNames { get; init; }
    public required string OptEmails { get; init; }
    public required string OptPhones { get; init; }
    public required string OptStreets { get; init; }
    public required string OptCities { get; init; }
    public required string OptIban { get; init; }
    public required string OptSsn { get; init; }
    public required string OptCards { get; init; }
    public required string OptRefs { get; init; }
    public required string OptPlates { get; init; }
    public required string OptOrgs { get; init; }
    public required string OptBirthdays { get; init; }
    public required string OptAllDates { get; init; }

    public required string CardTerms { get; init; }
    public required string TermsHint { get; init; }
    public required string TermsPlaceholder { get; init; }

    public required string CardLlm { get; init; }
    public required string LlmEnable { get; init; }
    public required string LlmHint { get; init; }
    public required string LlmStatusChecking { get; init; }
    public required string LlmStatusReady { get; init; }
    public required string LlmStatusOffline { get; init; }
    public required string LlmOfflineHelp { get; init; }
    public required string LlmModelLabel { get; init; }
    public required string LlmAnalyzing { get; init; }
    public required string LlmError { get; init; }
    public required string LlmStatusPulling { get; init; }    // {0} = Modell, {1} = Prozent
    public required string LlmPullNote { get; init; }
    public required string LlmPullFailed { get; init; }       // {0} = Modell
    public required string BtnRecheck { get; init; }
}

/// <summary>Hält die aktuelle Sprache einer Browser-Sitzung.</summary>
public sealed class LocalizationService
{
    public AppLanguage Language { get; private set; } = AppLanguage.De;
    public UiText T { get; private set; } = UiStrings.For(AppLanguage.De);

    public void Set(AppLanguage language)
    {
        Language = language;
        T = UiStrings.For(language);
    }

    public static readonly (AppLanguage Language, string Code, string NativeName)[] All =
    {
        (AppLanguage.De, "de", "Deutsch"),
        (AppLanguage.En, "en", "English"),
        (AppLanguage.Fr, "fr", "Français"),
        (AppLanguage.It, "it", "Italiano")
    };

    public static AppLanguage FromCode(string? code) => code switch
    {
        "en" => AppLanguage.En,
        "fr" => AppLanguage.Fr,
        "it" => AppLanguage.It,
        _ => AppLanguage.De
    };

    public static string ToCode(AppLanguage language) => language switch
    {
        AppLanguage.En => "en",
        AppLanguage.Fr => "fr",
        AppLanguage.It => "it",
        _ => "de"
    };
}

/// <summary>Die Übersetzungen für Deutsch, Englisch, Französisch und Italienisch.</summary>
public static class UiStrings
{
    public static UiText For(AppLanguage language) => language switch
    {
        AppLanguage.En => En,
        AppLanguage.Fr => Fr,
        AppLanguage.It => It,
        _ => De
    };

    private static readonly UiText De = new()
    {
        PageTitle = "Daten-Anonymisierer",
        AppTitle = "🔒 Daten-Anonymisierer",
        Tagline = "Ersetzt persönliche Daten (Namen, E-Mails, Telefonnummern, IBAN, AHV-Nr., Adressen, …) durch Platzhalter, bevor du einen Fall in Claude Code oder ein anderes KI-Tool einfügst. Alles läuft lokal – es werden keine Daten übertragen oder gespeichert.",
        CardOriginal = "1️⃣ Originaltext mit persönlichen Daten",
        InputPlaceholder = "Falltext hier einfügen, z.B.:\nHerr Max Muster (geb. 12.03.1985), Bahnhofstrasse 12, 8001 Zürich, Tel. +41 79 123 45 67, max.muster@example.ch, meldet einen Schaden. Policen-Nr. P-2023/4711, IBAN CH93 0076 2011 6238 5295 7.",
        BtnAnonymize = "🔒 Anonymisieren",
        BtnAnonymizeBusy = "⏳ Analysiere …",
        BtnClear = "Leeren",
        CardAnonymized = "2️⃣ Anonymisierter Text – sicher für Claude Code",
        BtnCopy = "📋 Kopieren",
        Copied = "✔ Kopiert",
        MappingTitle = "Zuordnungstabelle ({0} Werte ersetzt)",
        ThPlaceholder = "Platzhalter",
        ThOriginal = "Originalwert",
        ThCategory = "Kategorie",
        MappingNote = "Die Tabelle bleibt nur in dieser Browser-Sitzung im Speicher und wird nirgends abgelegt.",
        NoPiiFound = "Keine persönlichen Daten gefunden.",
        CardDeanonymize = "3️⃣ Antwort de-anonymisieren (Platzhalter zurückersetzen)",
        DeanonHint = "Antwort von Claude Code hier einfügen – die Platzhalter werden wieder durch die Originalwerte ersetzt.",
        ResponsePlaceholder = "Antwort mit Platzhaltern wie [NAME_1] hier einfügen …",
        BtnDeanonymize = "🔓 Platzhalter zurückersetzen",
        RestoredTitle = "Wiederhergestellter Text",
        CardRules = "⚙️ Erkennungsregeln",
        OptNames = "Namen (nach Herr/Frau, „Name:“, …)",
        OptEmails = "E-Mail-Adressen",
        OptPhones = "Telefonnummern",
        OptStreets = "Strassen & Hausnummern",
        OptCities = "PLZ & Ortschaften",
        OptIban = "IBAN",
        OptSsn = "AHV-/Sozialversicherungsnummern",
        OptCards = "Kreditkartennummern",
        OptRefs = "Kunden-/Policen-/Fall-Nummern",
        OptPlates = "Autokennzeichen (CH)",
        OptOrgs = "Firmen & Organisationen (nur mit KI)",
        OptBirthdays = "Geburtsdaten („geb.“, „geboren am“)",
        OptAllDates = "Alle Datumsangaben",
        CardTerms = "📝 Eigene Begriffe",
        TermsHint = "Ein Begriff pro Zeile (z.B. Firmennamen, Projektnamen oder Namen ohne Anrede). Diese werden immer ersetzt und lokal im Browser gespeichert.",
        TermsPlaceholder = "Muster AG\nAnna Beispiel\nProjekt Phoenix",
        CardLlm = "🤖 KI-Erkennung (lokales LLM)",
        LlmEnable = "Lokales LLM zusätzlich verwenden",
        LlmHint = "Ein lokal laufendes Sprachmodell (via Ollama) versteht, was persönliche Daten sind, und findet auch Namen ohne Anrede, Firmen und Ähnliches, das kein Muster abdeckt. Der Text bleibt dabei zu 100 % auf diesem Rechner.",
        LlmStatusChecking = "Prüfe Ollama …",
        LlmStatusReady = "✅ Ollama erreichbar",
        LlmStatusOffline = "⚠️ Ollama nicht erreichbar",
        LlmOfflineHelp = "Ollama von ollama.com installieren und starten – den Rest erledigt die App automatisch (das Modell wird selbständig heruntergeladen).",
        LlmModelLabel = "Modell",
        LlmAnalyzing = "Das lokale Modell analysiert den Text – das kann je nach Rechner eine Weile dauern.",
        LlmError = "KI-Erkennung fehlgeschlagen – das Ergebnis basiert nur auf den Erkennungsregeln.",
        LlmStatusPulling = "⬇️ Modell {0} wird heruntergeladen … {1} %",
        LlmPullNote = "Passiert nur einmal – danach ist die KI-Erkennung sofort einsatzbereit.",
        LlmPullFailed = "Automatischer Download fehlgeschlagen – im Terminal „ollama pull {0}“ ausführen.",
        BtnRecheck = "↻ Erneut prüfen"
    };

    private static readonly UiText En = new()
    {
        PageTitle = "Data Anonymizer",
        AppTitle = "🔒 Data Anonymizer",
        Tagline = "Replaces personal data (names, e-mails, phone numbers, IBANs, social security numbers, addresses, …) with placeholders before you paste a case into Claude Code or another AI tool. Everything runs locally – no data is transmitted or stored.",
        CardOriginal = "1️⃣ Original text with personal data",
        InputPlaceholder = "Paste your case text here, e.g.:\nMr. John Smith (born on 12/03/1985), 12 Main Street, Zurich, phone +41 79 123 45 67, john.smith@example.com, reports a claim. Policy No. P-2023/4711, IBAN CH93 0076 2011 6238 5295 7.",
        BtnAnonymize = "🔒 Anonymize",
        BtnAnonymizeBusy = "⏳ Analyzing …",
        BtnClear = "Clear",
        CardAnonymized = "2️⃣ Anonymized text – safe for Claude Code",
        BtnCopy = "📋 Copy",
        Copied = "✔ Copied",
        MappingTitle = "Mapping table ({0} values replaced)",
        ThPlaceholder = "Placeholder",
        ThOriginal = "Original value",
        ThCategory = "Category",
        MappingNote = "The table only lives in the memory of this browser session and is never stored anywhere.",
        NoPiiFound = "No personal data found.",
        CardDeanonymize = "3️⃣ De-anonymize the answer (restore placeholders)",
        DeanonHint = "Paste the answer from Claude Code here – the placeholders will be replaced with the original values again.",
        ResponsePlaceholder = "Paste the answer containing placeholders like [NAME_1] here …",
        BtnDeanonymize = "🔓 Restore original values",
        RestoredTitle = "Restored text",
        CardRules = "⚙️ Detection rules",
        OptNames = "Names (after Mr./Mrs., “Name:”, …)",
        OptEmails = "E-mail addresses",
        OptPhones = "Phone numbers",
        OptStreets = "Streets & house numbers",
        OptCities = "Postal codes & towns",
        OptIban = "IBAN",
        OptSsn = "Social security numbers (AHV/SSN)",
        OptCards = "Credit card numbers",
        OptRefs = "Customer/policy/case numbers",
        OptPlates = "License plates (CH)",
        OptOrgs = "Companies & organizations (AI only)",
        OptBirthdays = "Birth dates (“born on”, “DOB:”)",
        OptAllDates = "All dates",
        CardTerms = "📝 Custom terms",
        TermsHint = "One term per line (e.g. company names, project names or names without a salutation). These are always replaced and stored locally in your browser.",
        TermsPlaceholder = "Acme Ltd\nAnna Example\nProject Phoenix",
        CardLlm = "🤖 AI detection (local LLM)",
        LlmEnable = "Additionally use a local LLM",
        LlmHint = "A language model running locally (via Ollama) understands what personal data is and also finds names without salutations, companies and similar data that no pattern covers. Your text stays 100% on this machine.",
        LlmStatusChecking = "Checking Ollama …",
        LlmStatusReady = "✅ Ollama reachable",
        LlmStatusOffline = "⚠️ Ollama not reachable",
        LlmOfflineHelp = "Install Ollama from ollama.com and start it – the app does the rest automatically (the model is downloaded on its own).",
        LlmModelLabel = "Model",
        LlmAnalyzing = "The local model is analyzing the text – depending on your machine this can take a while.",
        LlmError = "AI detection failed – the result is based on the detection rules only.",
        LlmStatusPulling = "⬇️ Downloading model {0} … {1}%",
        LlmPullNote = "This happens only once – afterwards AI detection is ready right away.",
        LlmPullFailed = "Automatic download failed – run “ollama pull {0}” in a terminal.",
        BtnRecheck = "↻ Check again"
    };

    private static readonly UiText Fr = new()
    {
        PageTitle = "Anonymiseur de données",
        AppTitle = "🔒 Anonymiseur de données",
        Tagline = "Remplace les données personnelles (noms, e-mails, numéros de téléphone, IBAN, numéros AVS, adresses, …) par des variables avant de coller un cas dans Claude Code ou un autre outil d'IA. Tout fonctionne en local – aucune donnée n'est transmise ni enregistrée.",
        CardOriginal = "1️⃣ Texte original avec données personnelles",
        InputPlaceholder = "Collez votre texte ici, p. ex. :\nMonsieur Jean Dupont (né le 12.03.1985), Rue de Lausanne 12, 1201 Genève, tél. +41 79 123 45 67, jean.dupont@example.ch, annonce un sinistre. N° de police P-2023/4711, IBAN CH93 0076 2011 6238 5295 7.",
        BtnAnonymize = "🔒 Anonymiser",
        BtnAnonymizeBusy = "⏳ Analyse en cours …",
        BtnClear = "Effacer",
        CardAnonymized = "2️⃣ Texte anonymisé – sûr pour Claude Code",
        BtnCopy = "📋 Copier",
        Copied = "✔ Copié",
        MappingTitle = "Table de correspondance ({0} valeurs remplacées)",
        ThPlaceholder = "Variable",
        ThOriginal = "Valeur originale",
        ThCategory = "Catégorie",
        MappingNote = "La table ne réside que dans la mémoire de cette session du navigateur et n'est enregistrée nulle part.",
        NoPiiFound = "Aucune donnée personnelle trouvée.",
        CardDeanonymize = "3️⃣ Dé-anonymiser la réponse (restaurer les valeurs)",
        DeanonHint = "Collez ici la réponse de Claude Code – les variables seront remplacées par les valeurs originales.",
        ResponsePlaceholder = "Collez ici la réponse contenant des variables comme [NOM_1] …",
        BtnDeanonymize = "🔓 Restaurer les valeurs originales",
        RestoredTitle = "Texte restauré",
        CardRules = "⚙️ Règles de détection",
        OptNames = "Noms (après M./Mme, « Nom : », …)",
        OptEmails = "Adresses e-mail",
        OptPhones = "Numéros de téléphone",
        OptStreets = "Rues et numéros",
        OptCities = "Codes postaux et localités",
        OptIban = "IBAN",
        OptSsn = "Numéros AVS / sécurité sociale",
        OptCards = "Numéros de carte de crédit",
        OptRefs = "N° de client/police/dossier",
        OptPlates = "Plaques d'immatriculation (CH)",
        OptOrgs = "Entreprises et organisations (IA seulement)",
        OptBirthdays = "Dates de naissance (« né le », …)",
        OptAllDates = "Toutes les dates",
        CardTerms = "📝 Termes personnalisés",
        TermsHint = "Un terme par ligne (p. ex. noms d'entreprises, de projets ou noms sans civilité). Ils sont toujours remplacés et enregistrés localement dans votre navigateur.",
        TermsPlaceholder = "Exemple SA\nAnna Exemple\nProjet Phoenix",
        CardLlm = "🤖 Détection IA (LLM local)",
        LlmEnable = "Utiliser en plus un LLM local",
        LlmHint = "Un modèle de langue exécuté localement (via Ollama) comprend ce que sont les données personnelles et trouve aussi les noms sans civilité, les entreprises et d'autres données qu'aucun modèle ne couvre. Votre texte reste à 100 % sur cette machine.",
        LlmStatusChecking = "Vérification d'Ollama …",
        LlmStatusReady = "✅ Ollama accessible",
        LlmStatusOffline = "⚠️ Ollama inaccessible",
        LlmOfflineHelp = "Installez Ollama depuis ollama.com et démarrez-le – l'application s'occupe du reste (le modèle est téléchargé automatiquement).",
        LlmModelLabel = "Modèle",
        LlmAnalyzing = "Le modèle local analyse le texte – cela peut prendre un moment selon votre machine.",
        LlmError = "La détection IA a échoué – le résultat repose uniquement sur les règles de détection.",
        LlmStatusPulling = "⬇️ Téléchargement du modèle {0} … {1} %",
        LlmPullNote = "Cette étape n'a lieu qu'une seule fois – ensuite la détection IA est immédiatement prête.",
        LlmPullFailed = "Le téléchargement automatique a échoué – exécutez « ollama pull {0} » dans un terminal.",
        BtnRecheck = "↻ Revérifier"
    };

    private static readonly UiText It = new()
    {
        PageTitle = "Anonimizzatore di dati",
        AppTitle = "🔒 Anonimizzatore di dati",
        Tagline = "Sostituisce i dati personali (nomi, e-mail, numeri di telefono, IBAN, numeri AVS, indirizzi, …) con segnaposto prima di incollare un caso in Claude Code o in un altro strumento di IA. Tutto funziona in locale – nessun dato viene trasmesso o salvato.",
        CardOriginal = "1️⃣ Testo originale con dati personali",
        InputPlaceholder = "Incolla qui il testo, ad es.:\nSignor Mario Rossi (nato il 12.03.1985), Via Roma 8, 6900 Lugano, tel. +41 79 123 45 67, mario.rossi@example.ch, segnala un sinistro. Polizza n. P-2023/4711, IBAN CH93 0076 2011 6238 5295 7.",
        BtnAnonymize = "🔒 Anonimizza",
        BtnAnonymizeBusy = "⏳ Analisi in corso …",
        BtnClear = "Svuota",
        CardAnonymized = "2️⃣ Testo anonimizzato – sicuro per Claude Code",
        BtnCopy = "📋 Copia",
        Copied = "✔ Copiato",
        MappingTitle = "Tabella di corrispondenza ({0} valori sostituiti)",
        ThPlaceholder = "Segnaposto",
        ThOriginal = "Valore originale",
        ThCategory = "Categoria",
        MappingNote = "La tabella rimane solo nella memoria di questa sessione del browser e non viene salvata da nessuna parte.",
        NoPiiFound = "Nessun dato personale trovato.",
        CardDeanonymize = "3️⃣ De-anonimizzare la risposta (ripristinare i valori)",
        DeanonHint = "Incolla qui la risposta di Claude Code – i segnaposto verranno sostituiti di nuovo con i valori originali.",
        ResponsePlaceholder = "Incolla qui la risposta con segnaposto come [NOME_1] …",
        BtnDeanonymize = "🔓 Ripristina i valori originali",
        RestoredTitle = "Testo ripristinato",
        CardRules = "⚙️ Regole di riconoscimento",
        OptNames = "Nomi (dopo Sig./Sig.ra, «Nome:», …)",
        OptEmails = "Indirizzi e-mail",
        OptPhones = "Numeri di telefono",
        OptStreets = "Vie e numeri civici",
        OptCities = "CAP e località",
        OptIban = "IBAN",
        OptSsn = "Numeri AVS / previdenza sociale",
        OptCards = "Numeri di carta di credito",
        OptRefs = "N. cliente/polizza/pratica",
        OptPlates = "Targhe (CH)",
        OptOrgs = "Aziende e organizzazioni (solo IA)",
        OptBirthdays = "Date di nascita («nato il», …)",
        OptAllDates = "Tutte le date",
        CardTerms = "📝 Termini personalizzati",
        TermsHint = "Un termine per riga (ad es. nomi di aziende, di progetti o nomi senza titolo). Vengono sempre sostituiti e salvati localmente nel browser.",
        TermsPlaceholder = "Esempio SA\nAnna Esempio\nProgetto Phoenix",
        CardLlm = "🤖 Riconoscimento IA (LLM locale)",
        LlmEnable = "Usa anche un LLM locale",
        LlmHint = "Un modello linguistico eseguito localmente (via Ollama) capisce cosa sono i dati personali e trova anche nomi senza titolo, aziende e dati simili che nessun pattern copre. Il testo rimane al 100 % su questo computer.",
        LlmStatusChecking = "Verifica di Ollama …",
        LlmStatusReady = "✅ Ollama raggiungibile",
        LlmStatusOffline = "⚠️ Ollama non raggiungibile",
        LlmOfflineHelp = "Installa Ollama da ollama.com e avvialo – al resto pensa l'app (il modello viene scaricato automaticamente).",
        LlmModelLabel = "Modello",
        LlmAnalyzing = "Il modello locale sta analizzando il testo – a seconda del computer può richiedere un po' di tempo.",
        LlmError = "Riconoscimento IA non riuscito – il risultato si basa solo sulle regole di riconoscimento.",
        LlmStatusPulling = "⬇️ Download del modello {0} … {1}%",
        LlmPullNote = "Succede solo una volta – dopo il riconoscimento IA è subito pronto.",
        LlmPullFailed = "Download automatico non riuscito – esegui «ollama pull {0}» in un terminale.",
        BtnRecheck = "↻ Ricontrolla"
    };
}
