# Transactionimportflöde

Detta dokument beskriver vad som händer från att användaren klistrar in transaktionstext i Blazor-appen tills transaktionerna sparas i databasen.

## 1. Förhandsgranskning i klienten
- Användaren klistrar in text i `Import.razor` och klickar “Visa förhandsgranskning”.  
  - `PreviewImportAsync()` skapar en `text/plain`-förfrågan till `api/transactions/import/preview` med hela texten (`PiggyzenMvp.Blazor/Components/Pages/Transactions/Import.razor:252-290`).  
  - Svar innehåller kolumnförslag, förhandsgranskningsrader och eventuella parsingfel.
- Användaren markerar vilken kolumn som innehåller datum, beskrivning, belopp etc. och klickar “Importera” efter att ha verifierat preview (`Import.razor:293-340`).
  - `ImportTransactions()` bygger `TransactionImportWithSchemaRequest` (råtext + schema från `UpdateSchemaDefinitionFromSelection`) och POSTar till `api/transactions/import/schema`.

## 2. Server: preview och schemaanalys
- `TransactionsController.PreviewImport()` tar emot råtexten, anropar `TransactionImportService.PreparePreview()` och returnerar `TransactionImportPreviewResult` (`PiggyzenMvp.API/Controllers/TransactionsController.cs:95-113`).  
  - `PreparePreview()` normaliserar radslut (`NormalizeLineEndings`), separerar rader (`SplitIntoRows`), detekterar separator och kolumnantal (`TryDetectLayout`), bygger schema och preview (`TryBuildSchema`, `BuildPreviewRows`), och paketerar kolumnhintar och ignorerade rader.

## 3. Server: parse och validering
- `TransactionsController.ImportWithSchema()` validerar requesten, anropar `TransactionImportService.ParseWithSchema()` och skickar resultat till `ImportParsedTransactionsAsync()` (`TransactionsController.cs:115-301`).  
  - `ParseWithSchema()` kontrollerar kolumnantal, verifierar schemat (`TransactionImportSchemaDefinition.TryValidate`) och kör `ParseRows()` över giltiga rader (`PiggyzenMvp.API/Services/TransactionImportService.cs:185-243`).  
  - `ParseRows()` validerar datum och belopp (`TryValidateBasic`, `TryParseDate`, `TryParseAmount`), normaliserar beskrivningen (`NormalizeService.Normalize`), bygger `TransactionImportDto` per rad och fyller på parsingfel vid behov (`TransactionImportService.cs:766-837`).

## 4. Server: sparande och import-logik
- `ImportParsedTransactionsAsync()` (i controller) tar DTO-listan och:
  1. Grupperar på fingerprint (datum + normaliserad beskrivning + belopp) och markerar dubletter (`TransactionsController.cs:137-218`).  
  2. Genererar unika import-id per rad, mappar `TypeRaw` via `TransactionKindMapper.Map()` (`PiggyzenMvp.API/Services/TransactionKindMapper.cs:1-68`), och skapar/updaterar beskrivningssignaturer via `DescriptionSignatureService.GetOrCreateAsync()` (`PiggyzenMvp.API/Services/DescriptionSignatureService.cs:1-131`).  
  3. Bygger `Transaction`-entiteter (`PiggyzenMvp.API/Models/Transaction.cs:1-25`), sparar dem inom en transaktion och kör `CategorizationService.AutoCategorizeBatchAsync` för auto-kategorisering vid nyimport (`TransactionsController.cs:227-301`).  
  4. Returnerar `ImportResult` med importerade DTO:er, parsingfel, dublettvarningar och eventuella auto-kategoriseringsresultat till klienten.

## 5. Klient: resultatpresentation
- Blazor-komponentens state uppdateras efter import: tabellen `parsedTransactions`, listor med fel/varningar och `autoCategorizedCount`/`autoCategorizeErrors` visas enligt serverns svar (`Import.razor`, sektionerna efter `@code`).  
- Eventuella fel i HTTP-svar läggs i `errorMessage` via `PopulateErrorFromResponse()` för att visa detaljerad diagnostik.

Vill du att jag placerar denna dokumentation på en annan plats, eller ska vi bygga ut den med sekvensdiagram eller annat visuellt material?  
