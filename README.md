ProtoGenerator - Visual Studio Extension

Descrizione

ProtoGenerator è un'estensione per Visual Studio che consente di convertire classi C# in file .proto per Protobuf. Questa estensione aggiunge un'opzione nel menu contestuale del Solution Explorer, permettendo di generare rapidamente i file .proto da modelli C#.

Funzionalità

Analizza automaticamente le classi C# e le loro proprietà.

Converte le classi in file .proto compatibili con Protocol Buffers.

Integra un'opzione nel menu contestuale del Solution Explorer per la conversione rapida.

Supporto per .NET Framework 4.7.2.

Requisiti

Visual Studio 2019 o 2022

.NET Framework 4.7.2

Roslyn SDK per l'analisi del codice sorgente

Installazione

Installazione manuale

Scaricare il file .vsix dalla sezione Releases.

Eseguire il file .vsix e seguire le istruzioni di installazione.

Riavviare Visual Studio per abilitare l'estensione.

Installazione da Visual Studio Marketplace

L'estensione sarà disponibile sul Visual Studio Marketplace (inserisci il link quando disponibile).

Utilizzo

Aprire un progetto in Visual Studio.

Fare clic con il tasto destro sulla classe C# che si desidera convertire.

Selezionare l'opzione Convert to Proto dal menu contestuale.

Il file .proto verrà generato nella stessa cartella della classe.

Struttura del progetto

ProtoGenerator/
│-- src/
│   │-- CSharpClassParser.cs
│   │-- ProtoGeneratorPackage.cs
│   │-- ProtoFileWriter.cs
│-- vsix/
│   │-- source.extension.vsixmanifest
│-- README.md
│-- LICENSE

Contributi

I contributi sono benvenuti! Sentiti libero di aprire una issue o inviare una pull request.
