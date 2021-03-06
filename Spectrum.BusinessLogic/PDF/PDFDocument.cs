﻿using Clock.Hocr;
using iTextSharp.text.pdf;
using System;
using System.Collections.Generic;
using System.IO;
using TesseractUI.BusinessLogic.FileSystem;
using TesseractUI.BusinessLogic.HOCR;
using TesseractUI.BusinessLogic.Images;
using TesseractUI.BusinessLogic.PDF;
using TesseractUI.BusinessLogic.ProcessAccess;
using TesseractUI.BusinessLogic.Tesseract;

namespace TesseractUI.BusinessLogic
{
    public class PDFDocument
    {
        #region Fields
        private string _FilePath;
        private IFileSystem _FileSystem;
        #endregion

        public PDFDocument(string filePath)
        {
            this._FilePath = filePath;
        }

        public void Ocr(Language recognitionLanguage)
        {
            this._FileSystem = new FileSystemAccess(this._FilePath);

            IPDFAccess pdf = new ITextSharpPDFAccess(this._FileSystem, this._FilePath);
            PDFImageGenerator imageGenerator = new PDFImageGenerator();

            List<string> pdfImages = GeneratePDFImages(
                this._FileSystem, pdf, this._FilePath, this._FileSystem.TemporaryWorkingDirectory);

            IHOCRDocument hocrDocument = new hDocument();
            string tesseractLanguageString = 
                new LanguageMapping().GetTesseractLanguageStringFromEnumeration(recognitionLanguage);

            IHOCRDocument ocrDocument = new HOCRFileCreator().
                CreateHOCROfImages(hocrDocument, new Parser(),
                this._FileSystem, new TesseractProgram(), new ProcessStarter(), pdfImages, tesseractLanguageString);
            
            AddOcrContent(this._FileSystem, pdf, ocrDocument, 300);
        }

        public void DeleteTemporaryFiles()
        {
            this._FileSystem.DeleteTemporaryWorkingDirectory();
        }

        public void SaveToPath(string targetPath)
        {
            try
            {
                File.Copy(this._FileSystem.DestinationPDFPath, targetPath, true);
            }
            catch (Exception)
            {

            }
        }

        public void AddOcrContent(IFileSystem fileSystem, IPDFAccess pdf, IHOCRDocument ocrDocument, int Dpi, string FontName = null)
        {
            var mem = new FileStream(fileSystem.DestinationPDFPath, FileMode.Create, FileAccess.ReadWrite);
            PdfStamper pdfStamper = new PdfStamper(pdf.ReaderObject, mem);

            int pageCounter = 1;
            foreach (hPage hrPage in ocrDocument.Pages)
            {
                PdfImportedPage page = pdfStamper.GetImportedPage(pdf.ReaderObject, pageCounter);

                foreach (hParagraph para in hrPage.Paragraphs)
                {
                    foreach (hLine line in para.Lines)
                    {
                        line.AlignTops();

                        foreach (hWord c in line.Words)
                        {
                            c.CleanText();

                            BBox b = BBox.ConvertBBoxToPoints(c.BBox, Dpi);

                            if (b.Height > 50)
                                continue;
                            PdfContentByte cb = pdfStamper.GetUnderContent(pageCounter);

                            BaseFont base_font = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.WINANSI, false);

                            iTextSharp.text.Font font = new iTextSharp.text.Font(base_font);
                            if (FontName != null && FontName != string.Empty)
                            {
                                var fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), FontName);
                                base_font = BaseFont.CreateFont(fontPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
                                // BaseFont base_font = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.WINANSI, false);
                                font = new iTextSharp.text.Font(base_font);
                            }

                            cb.BeginText();

                            float size = 0;// Math.Round(b.Height);
                            while (1 == 1 && size < 50)
                            {
                                var width = base_font.GetWidthPoint(c.Text, size);
                                if (width < b.Width)
                                {
                                    size += 1;
                                }
                                else
                                    break;
                            }
                            if (size < 10)
                                size = size - 1;

                            if (size == 0)
                                size = 1;

                            cb.SetFontAndSize(base_font, b.Height >= 2 ? (int)size : 2);
                            cb.SetTextMatrix(b.Left, page.Height - b.Top - b.Height);
                            cb.SetWordSpacing(PdfWriter.SPACE);

                            cb.ShowText(c.Text + " ");
                            cb.EndText();
                        }
                    }
                }

                pageCounter++;

                pdf.RemoveUnusedObjects();
            }


            pdfStamper.Close();
            pdfStamper.Reader.Close();
            mem.Close();

            mem = null;
            pdf = null;
        }

        private List<string> GeneratePDFImages(IFileSystem fileSystem, IPDFAccess pdf, string filePath, string outputPath)
        {
            List<string> pdfImages = new List<string>();
            PDFImageGenerator imageGenerator = new PDFImageGenerator();

            for (int pageNumber = 1; pageNumber <= pdf.NumberOfPages; pageNumber++)
            {
                pdfImages.Add(
                    imageGenerator.GeneratePageImage(fileSystem, pdf, filePath, pageNumber, outputPath));
            }

            return pdfImages;
        }
    }
}
