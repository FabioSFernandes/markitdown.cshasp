# Dependencies and licenses

This project is **MIT-licensed**. Third-party dependencies are MIT or Apache 2.0. Use of Apache 2.0 libraries does not change the project license; those components remain under Apache 2.0 (see their NOTICE where applicable).

## Direct package references

| Package | Version | License |
|---------|---------|---------|
| ClosedXML | 0.105.0 | MIT |
| DocumentFormat.OpenXml | 3.3.0 | Apache 2.0 |
| ExcelDataReader | 3.8.0 | MIT |
| ExcelDataReader.DataSet | 3.8.0 | MIT |
| HtmlAgilityPack | 1.12.4 | MIT |
| Microsoft.AspNetCore.StaticFiles | 2.3.0 | MIT (Microsoft) |
| Microsoft.AspNetCore.WebUtilities | 8.0.11 / 9.0.0 / 10.0.0 | MIT (Microsoft) |
| MsgReader | 6.0.6 | MIT |
| ReverseMarkdown | 4.7.1 | MIT |
| System.Text.Encoding.CodePages | 10.0.0 | MIT (Microsoft) |
| Tesseract | 5.2.0 | Apache 2.0 |
| Ude.NetStandard | 1.2.0 | MIT |
| UglyToad.PdfPig | 1.7.0-custom-5 | Apache 2.0 |

## Format support

- **DOCX** — supported (built-in extraction from OOXML; math pre-processing via ConverterUtils.Docx).
- **XLSX** — supported (ClosedXML).
- **XLS** — supported (ExcelDataReader).
- **PDF** — supported (UglyToad.PdfPig).
- **PPTX** — supported (DocumentFormat.OpenXml).
- **Images** — supported; optional **Tesseract** OCR when `tesseract_tessdata_path` is set (requires `tessdata` folder with e.g. `eng.traineddata`).
