// Generates sample contract PDFs with overlapping clause types — termination,
// indemnification, liability cap, confidentiality, payment — but deliberately
// DIFFERENT terms, so comparison questions have real answers. Run from repo root:
//   dotnet run --project tools/SampleContractGenerator            -> /sample-contracts (seed set: NDA, MSA, SaaS)
//   dotnet run --project tools/SampleContractGenerator -- --extra -> /test-pdfs (upload-test contracts + edge cases)
//   dotnet run --project tools/SampleContractGenerator -- --check -> extract + chunk sanity check on both folders
// The seed set is bundled with the API for /api/seed and asserted by the QA
// suite — new test documents belong in /test-pdfs, not /sample-contracts.
using ContractClause.Shared;
using ContractClause.Shared.Pdf;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace SampleContractGenerator;

static class Program
{
    static async Task Main(string[] args)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var seedDir = Path.GetFullPath("sample-contracts");
        var extraDir = Path.GetFullPath("test-pdfs");

        if (args.Contains("--check"))
            Generator.CheckExtraction(seedDir, extraDir);
        else if (args.Contains("--extra"))
            await Generator.WriteExtraSetAsync(extraDir);
        else
            Generator.WriteSeedSet(seedDir);
    }
}

static class Generator
{
    public static void CheckExtraction(string seedDir, string extraDir)
    {
        foreach (var dir in new[] { seedDir, extraDir }.Where(Directory.Exists))
        {
            foreach (var pdf in Directory.GetFiles(dir, "*.pdf"))
            {
                CheckPdf(pdf);
            }
        }
    }

    static void CheckPdf(string pdf)
    {
        try
        {
            using var stream = File.OpenRead(pdf);
            var pages = PdfTextExtractor.Extract(stream);
            var chunks = ClauseChunker.Chunk(pages).Where(c => !string.IsNullOrWhiteSpace(c.Text)).ToList();
            Console.WriteLine($"\n=== {Path.GetFileName(pdf)} — {pages.Count} pages, {chunks.Count} clauses ===");
            foreach (var c in chunks)
                Console.WriteLine($"  [{c.ClauseNumber,-4}] p{c.PageNumber} {c.ClauseTitle,-42} {c.Text.Length,5} chars");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n=== {Path.GetFileName(pdf)} — UNREADABLE: {ex.Message} ===");
        }
    }

    public static void WriteSeedSet(string seedDir)
    {
        Directory.CreateDirectory(seedDir);
        foreach (var contract in SampleContracts.All)
            WriteContract(seedDir, contract);
    }

    public static async Task WriteExtraSetAsync(string extraDir)
    {
        Directory.CreateDirectory(extraDir);
        foreach (var contract in ExtraTestContracts.All)
            WriteContract(extraDir, contract);
        await WriteEdgeCasesAsync(extraDir);
    }

    // edge cases for the failure paths: an empty file should make the upload
    // endpoint respond with a 400, and a PDF without a text layer (scanned-style)
    // should make ingestion fail with "No clauses extracted" and leave the
    // contract row on status Failed
    static async Task WriteEdgeCasesAsync(string extraDir)
    {
        var emptyPath = Path.Combine(extraDir, "edge-empty.pdf");
        await File.WriteAllBytesAsync(emptyPath, []);
        Console.WriteLine($"wrote {emptyPath}");
        var scannedPath = Path.Combine(extraDir, "edge-scanned-no-text.pdf");
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(54);
                page.Content().Column(col =>
                {
                    for (var i = 0; i < 30; i++)
                        col.Item().PaddingBottom(8).Height(i % 8 == 0 ? 22 : 13)
                            .Background(i % 8 == 0 ? "#C9C9C9" : "#E6E6E6");
                });
            });
        }).GeneratePdf(scannedPath);
        Console.WriteLine($"wrote {scannedPath}");
    }

    static void WriteContract(string dir, ContractDoc contract)
    {
        var path = Path.Combine(dir, contract.FileName);
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(54);
                page.DefaultTextStyle(t => t.FontSize(10.5f).FontFamily("Times New Roman").LineHeight(1.35f));
                page.Content().Column(col =>
                {
                    col.Item().PaddingBottom(14).AlignCenter()
                        .Text(contract.Title).FontSize(15).Bold();
                    foreach (var line in contract.Preamble)
                        col.Item().PaddingBottom(8).Text(line).Justify();
                    foreach (var section in contract.Sections)
                    {
                        col.Item().PaddingTop(10).PaddingBottom(4)
                            .Text($"{section.Number}. {section.Title}").FontSize(11.5f).Bold();
                        foreach (var paragraph in section.Paragraphs)
                            col.Item().PaddingBottom(6).Text(paragraph).Justify();
                    }
                    col.Item().PaddingTop(24).Text(
                        "IN WITNESS WHEREOF, the parties have executed this Agreement as of the Effective Date.");
                });
            });
        }).GeneratePdf(path);
        Console.WriteLine($"wrote {path}");
    }
}

record Section(string Number, string Title, params string[] Paragraphs);
record ContractDoc(string FileName, string Title, string[] Preamble, Section[] Sections);

// Section titles shared by every contract — kept identical so comparison
// questions line up on the same clause names across documents.
static class ClauseTitles
{
    public const string Indemnification = "Indemnification";
    public const string LimitationOfLiability = "Limitation of Liability";
}

static class SampleContracts
{
    public static readonly ContractDoc[] All =
    [
        new ContractDoc(
            "Mutual-NDA-Meridian-Bluewater.pdf",
            "MUTUAL NON-DISCLOSURE AGREEMENT",
            [
                "This Mutual Non-Disclosure Agreement (the \"Agreement\") is entered into as of March 3, 2026 (the \"Effective Date\") by and between Meridian Software Labs, Inc., a Delaware corporation with offices at 410 Harbor Way, Wilmington, DE (\"Meridian\"), and Bluewater Analytics LLC, a Colorado limited liability company with offices at 88 Summit Street, Denver, CO (\"Bluewater\"). Each party may be a \"Disclosing Party\" or a \"Receiving Party\" under this Agreement.",
                "The parties wish to explore a potential business relationship concerning joint development of analytics tooling (the \"Purpose\") and, in connection with the Purpose, each party may disclose certain confidential and proprietary information to the other.",
            ],
            [
                new Section("1", "Definitions",
                    "1.1 \"Confidential Information\" means any non-public information disclosed by the Disclosing Party to the Receiving Party, whether orally, in writing, or by inspection of tangible objects, that is designated as confidential or that reasonably should be understood to be confidential given the nature of the information and the circumstances of disclosure, including without limitation business plans, financial data, customer lists, product roadmaps, source code, and pricing information.",
                    "1.2 \"Representatives\" means a party's employees, officers, directors, advisors, and contractors who have a need to know Confidential Information for the Purpose and who are bound by confidentiality obligations at least as protective as those in this Agreement."),
                new Section("2", "Obligations of the Receiving Party",
                    "2.1 The Receiving Party shall hold all Confidential Information in strict confidence, shall not disclose it to any third party other than its Representatives, and shall use it solely for the Purpose.",
                    "2.2 The Receiving Party shall protect Confidential Information using at least the same degree of care it uses to protect its own confidential information, and in no event less than reasonable care.",
                    "2.3 The Receiving Party shall promptly notify the Disclosing Party in writing upon discovery of any unauthorized use or disclosure of Confidential Information."),
                new Section("3", "Exclusions",
                    "Confidential Information does not include information that: (a) is or becomes publicly available through no fault of the Receiving Party; (b) was rightfully known to the Receiving Party without restriction before disclosure; (c) is rightfully received from a third party without breach of any obligation of confidentiality; or (d) is independently developed by the Receiving Party without use of or reference to the Disclosing Party's Confidential Information."),
                new Section("4", "Compelled Disclosure",
                    "If the Receiving Party is required by law, regulation, or court order to disclose Confidential Information, it shall, to the extent legally permitted, give the Disclosing Party prompt written notice and reasonable assistance so that the Disclosing Party may seek a protective order or other appropriate remedy."),
                new Section("5", "Term and Termination",
                    "5.1 This Agreement commences on the Effective Date and continues for two (2) years, unless terminated earlier by either party upon thirty (30) days' prior written notice to the other party.",
                    "5.2 The Receiving Party's obligations with respect to Confidential Information disclosed during the term shall survive termination or expiration of this Agreement for a period of five (5) years from the date of disclosure, except that obligations with respect to trade secrets shall continue for as long as such information remains a trade secret under applicable law."),
                new Section("6", "Return or Destruction of Materials",
                    "Upon the Disclosing Party's written request or upon termination of this Agreement, the Receiving Party shall promptly return or destroy all copies of Confidential Information in its possession, and upon request certify such destruction in writing, except for one archival copy retained solely to monitor compliance with this Agreement or as required by law."),
                new Section("7", "No License or Warranty",
                    "7.1 No license under any patent, copyright, trademark, or other intellectual property right is granted or implied by the disclosure of Confidential Information under this Agreement.",
                    "7.2 ALL CONFIDENTIAL INFORMATION IS PROVIDED \"AS IS\" WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED."),
                new Section("8", "Remedies",
                    "The parties acknowledge that unauthorized disclosure of Confidential Information may cause irreparable harm for which monetary damages would be an inadequate remedy. Accordingly, the Disclosing Party shall be entitled to seek injunctive or other equitable relief in addition to all other remedies available at law, without the requirement to post a bond."),
                new Section("9", ClauseTitles.Indemnification,
                    "Each party shall indemnify and hold harmless the other party from and against any third-party claims, damages, and reasonable attorneys' fees arising out of the indemnifying party's breach of its confidentiality obligations under this Agreement, provided that the indemnified party gives prompt written notice of the claim and reasonable cooperation in its defense."),
                new Section("10", ClauseTitles.LimitationOfLiability,
                    "EXCEPT FOR A BREACH OF SECTION 2 (OBLIGATIONS OF THE RECEIVING PARTY) OR AMOUNTS PAYABLE UNDER SECTION 9 (INDEMNIFICATION), NEITHER PARTY'S AGGREGATE LIABILITY ARISING OUT OF OR RELATED TO THIS AGREEMENT SHALL EXCEED FIVE HUNDRED THOUSAND U.S. DOLLARS (US $500,000), AND NEITHER PARTY SHALL BE LIABLE FOR ANY INDIRECT, INCIDENTAL, SPECIAL, OR CONSEQUENTIAL DAMAGES."),
                new Section("11", "Governing Law",
                    "This Agreement shall be governed by and construed in accordance with the laws of the State of Delaware, without regard to its conflict of laws principles. The state and federal courts located in New Castle County, Delaware shall have exclusive jurisdiction over any dispute arising under this Agreement."),
                new Section("12", "Miscellaneous",
                    "This Agreement constitutes the entire agreement between the parties regarding its subject matter and supersedes all prior discussions. It may be amended only in a writing signed by both parties. Neither party may assign this Agreement without the prior written consent of the other, except to a successor in connection with a merger or sale of substantially all assets. If any provision is held unenforceable, the remainder shall continue in full force."),
            ]),

        new ContractDoc(
            "MSA-Meridian-Northgate.pdf",
            "MASTER SERVICES AGREEMENT",
            [
                "This Master Services Agreement (the \"Agreement\") is entered into as of January 15, 2026 (the \"Effective Date\") by and between Meridian Software Labs, Inc., a Delaware corporation (\"Provider\"), and Northgate Financial Group, Inc., a New York corporation (\"Client\").",
                "Provider will perform professional software development and consulting services for Client pursuant to one or more statements of work executed under this Agreement (each, a \"Statement of Work\" or \"SOW\").",
            ],
            [
                new Section("1", "Services",
                    "1.1 Provider shall perform the services described in each SOW (the \"Services\") in a professional and workmanlike manner, using personnel with suitable skill and experience.",
                    "1.2 Each SOW shall describe the Services, deliverables, schedule, fees, and any acceptance criteria. In the event of a conflict between a SOW and this Agreement, the SOW governs for that engagement only if it expressly references the conflicting section.",
                    "1.3 Changes to a SOW require a written change order signed by both parties describing the impact on scope, schedule, and fees."),
                new Section("2", "Term",
                    "This Agreement begins on the Effective Date and continues for an initial term of three (3) years. Thereafter it automatically renews for successive one (1) year periods unless either party gives written notice of non-renewal at least ninety (90) days before the end of the then-current term."),
                new Section("3", "Fees and Payment",
                    "3.1 Client shall pay the fees set forth in each SOW. Unless a SOW states otherwise, Services are billed monthly in arrears on a time-and-materials basis.",
                    "3.2 Invoices are due and payable within thirty (30) days of the invoice date (net 30). Late payments accrue interest at one and one-half percent (1.5%) per month or the maximum rate permitted by law, whichever is less.",
                    "3.3 Client is responsible for all applicable sales, use, and value-added taxes, excluding taxes on Provider's income. Pre-approved, reasonable travel expenses are reimbursable at cost."),
                new Section("4", "Termination",
                    "4.1 Termination for Convenience. Client may terminate this Agreement or any SOW for convenience upon sixty (60) days' prior written notice to Provider, and shall pay for all Services performed and non-cancellable commitments incurred through the effective date of termination.",
                    "4.2 Termination for Cause. Either party may terminate this Agreement if the other party materially breaches it and fails to cure the breach within thirty (30) days after receiving written notice describing the breach.",
                    "4.3 Effect of Termination. Sections 5 (Confidentiality), 6 (Intellectual Property), 8 (Indemnification), 9 (Limitation of Liability), and 12 (Governing Law) survive termination."),
                new Section("5", "Confidentiality",
                    "5.1 Each party agrees to protect the other party's non-public business, technical, and financial information (\"Confidential Information\") with reasonable care, to use it only to perform under this Agreement, and not to disclose it except to personnel bound by equivalent obligations.",
                    "5.2 These confidentiality obligations survive for three (3) years after termination or expiration of this Agreement, except for trade secrets, which remain protected for as long as they qualify as trade secrets."),
                new Section("6", "Intellectual Property",
                    "6.1 Upon full payment, all deliverables expressly identified in a SOW are assigned to Client and become Client's property (\"Work Product\").",
                    "6.2 Provider retains ownership of its pre-existing materials, tools, and generic know-how (\"Provider Materials\"). Provider grants Client a perpetual, non-exclusive, royalty-free license to use Provider Materials solely as incorporated into the Work Product."),
                new Section("7", "Warranties",
                    "7.1 Provider warrants that the Services will materially conform to the applicable SOW for ninety (90) days following delivery. Client's exclusive remedy for breach of this warranty is re-performance of the non-conforming Services or, if re-performance fails, a refund of fees paid for those Services.",
                    "7.2 EXCEPT AS EXPRESSLY STATED IN THIS SECTION, PROVIDER DISCLAIMS ALL OTHER WARRANTIES, EXPRESS OR IMPLIED, INCLUDING MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE."),
                new Section("8", ClauseTitles.Indemnification,
                    "8.1 Provider shall defend and indemnify Client against third-party claims alleging that the Work Product, as delivered, infringes a U.S. patent, copyright, or trade secret, and shall pay resulting damages finally awarded, provided Client gives prompt notice and sole control of the defense to Provider.",
                    "8.2 Provider's indemnity does not apply to claims arising from Client's modifications, combination with materials not supplied by Provider, or use after Provider provides a non-infringing alternative.",
                    "8.3 Each party shall indemnify the other against third-party claims for bodily injury, death, or damage to tangible property to the extent caused by its negligence or willful misconduct."),
                new Section("9", ClauseTitles.LimitationOfLiability,
                    "9.1 EXCEPT FOR INDEMNIFICATION OBLIGATIONS UNDER SECTION 8, BREACH OF SECTION 5 (CONFIDENTIALITY), OR A PARTY'S GROSS NEGLIGENCE OR WILLFUL MISCONDUCT, EACH PARTY'S TOTAL AGGREGATE LIABILITY UNDER THIS AGREEMENT SHALL NOT EXCEED THE FEES PAID OR PAYABLE BY CLIENT TO PROVIDER IN THE TWELVE (12) MONTHS PRECEDING THE EVENT GIVING RISE TO THE CLAIM.",
                    "9.2 NEITHER PARTY IS LIABLE FOR LOST PROFITS, LOSS OF DATA, OR INDIRECT, INCIDENTAL, SPECIAL, PUNITIVE, OR CONSEQUENTIAL DAMAGES, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGES."),
                new Section("10", "Insurance",
                    "During the term, Provider shall maintain commercial general liability insurance of at least $1,000,000 per occurrence and $2,000,000 in the aggregate, professional liability (errors and omissions) insurance of at least $2,000,000, and statutory workers' compensation coverage, and shall provide certificates of insurance upon Client's reasonable request."),
                new Section("11", "Force Majeure",
                    "Neither party is liable for delay or failure to perform (other than payment obligations) caused by events beyond its reasonable control, including natural disasters, war, terrorism, labor disputes, governmental action, or failures of third-party utilities or networks, provided the affected party gives prompt notice and uses commercially reasonable efforts to mitigate."),
                new Section("12", "Governing Law and Disputes",
                    "This Agreement is governed by the laws of the State of New York, excluding its conflict of laws rules. The parties consent to the exclusive jurisdiction of the state and federal courts located in New York County, New York. Before filing suit, the parties shall attempt in good faith to resolve any dispute through senior-executive negotiation for at least thirty (30) days."),
                new Section("13", "General",
                    "Provider is an independent contractor; nothing in this Agreement creates a partnership, joint venture, or employment relationship. Notices must be in writing and delivered to the addresses on the signature page. Neither party may assign this Agreement without the other's prior written consent, not to be unreasonably withheld. This Agreement, together with all SOWs, is the entire agreement of the parties."),
            ]),

        new ContractDoc(
            "SaaS-Agreement-Cloudhaven-Northgate.pdf",
            "SOFTWARE AS A SERVICE SUBSCRIPTION AGREEMENT",
            [
                "This Software as a Service Subscription Agreement (the \"Agreement\") is made as of February 1, 2026 (the \"Effective Date\") between Cloudhaven Technologies, Inc., a California corporation (\"Vendor\"), and Northgate Financial Group, Inc., a New York corporation (\"Customer\").",
                "Vendor provides a cloud-hosted portfolio reporting platform (the \"Service\"), and Customer wishes to subscribe to the Service subject to the terms below.",
            ],
            [
                new Section("1", "Subscription and Access",
                    "1.1 Vendor grants Customer a non-exclusive, non-transferable right during the Subscription Term for up to two hundred fifty (250) named users to access and use the Service for Customer's internal business purposes.",
                    "1.2 Customer shall not (a) sublicense or resell the Service, (b) reverse engineer the Service except as permitted by law, (c) use the Service to develop a competing product, or (d) exceed the purchased user count without an upgrade order."),
                new Section("2", "Service Levels and Support",
                    "2.1 Vendor will make the Service available at least 99.9% of each calendar month, excluding scheduled maintenance announced at least forty-eight (48) hours in advance (the \"Uptime Commitment\").",
                    "2.2 If monthly availability falls below the Uptime Commitment, Customer is entitled to a service credit of 5% of the monthly subscription fee for each full 0.1% of availability shortfall, capped at 30% of the monthly fee. Service credits are Customer's sole and exclusive remedy for availability failures.",
                    "2.3 Vendor provides support via email and portal during business hours (8:00–18:00 Eastern, weekdays), with a four (4) hour initial response target for severity-1 incidents."),
                new Section("3", "Fees and Payment",
                    "3.1 Customer shall pay the annual subscription fee stated in the order form, invoiced annually in advance. Invoices are due within forty-five (45) days of the invoice date (net 45).",
                    "3.2 On each renewal, Vendor may increase subscription fees by no more than five percent (5%) over the prior term's fees, with at least sixty (60) days' prior written notice.",
                    "3.3 Fees are non-refundable except as expressly provided in this Agreement. Amounts more than thirty (30) days overdue accrue interest at 1% per month, and Vendor may suspend access for accounts more than sixty (60) days overdue after written notice."),
                new Section("4", "Term and Termination",
                    "4.1 The initial Subscription Term is one (1) year from the Effective Date and automatically renews for successive one (1) year terms unless either party gives written notice of non-renewal at least ninety (90) days before the end of the then-current term.",
                    "4.2 Either party may terminate this Agreement for material breach if the breach is not cured within thirty (30) days after written notice. Customer may also terminate if a severity-1 outage exceeds seventy-two (72) consecutive hours, in which case Vendor shall refund prepaid fees for the unused portion of the Subscription Term.",
                    "4.3 Upon termination, Customer's access ceases, and Vendor shall make Customer Data available for export in CSV format for thirty (30) days, after which Vendor shall delete Customer Data within ninety (90) days, except as retained in routine backups."),
                new Section("5", "Customer Data, Security and Data Protection",
                    "5.1 Customer retains all right, title, and interest in data submitted to the Service (\"Customer Data\"). Vendor may use Customer Data only to provide and improve the Service and as instructed by Customer.",
                    "5.2 Vendor shall maintain an information security program aligned with SOC 2 Type II, including encryption of Customer Data in transit (TLS 1.2 or higher) and at rest (AES-256), role-based access controls, and annual penetration testing.",
                    "5.3 Vendor shall notify Customer without undue delay, and in any case within seventy-two (72) hours, after becoming aware of a security breach affecting Customer Data, and shall provide reasonable cooperation in Customer's incident response."),
                new Section("6", "Confidentiality",
                    "6.1 Each party shall protect the other's Confidential Information with at least reasonable care, use it only as needed to perform under this Agreement, and disclose it only to personnel and advisors under equivalent confidentiality obligations.",
                    "6.2 Confidentiality obligations survive for five (5) years following termination or expiration of this Agreement; obligations for trade secrets survive as long as trade secret protection applies."),
                new Section("7", "Intellectual Property",
                    "The Service, including all software, interfaces, and documentation, is the exclusive property of Vendor and its licensors. No rights are granted except as expressly set forth in this Agreement. Customer grants Vendor a limited license to host and process Customer Data solely to provide the Service. Vendor may use aggregated, de-identified usage statistics that do not identify Customer or any individual."),
                new Section("8", "Warranties and Disclaimers",
                    "8.1 Vendor warrants that the Service will perform materially in accordance with its published documentation and that it will not materially degrade the functionality of the Service during a Subscription Term.",
                    "8.2 EXCEPT AS STATED ABOVE, THE SERVICE IS PROVIDED \"AS IS\" AND VENDOR DISCLAIMS ALL OTHER WARRANTIES, INCLUDING IMPLIED WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE, AND NON-INFRINGEMENT. VENDOR DOES NOT WARRANT THAT THE SERVICE WILL BE UNINTERRUPTED OR ERROR-FREE."),
                new Section("9", ClauseTitles.Indemnification,
                    "9.1 Vendor shall defend Customer against third-party claims that the Service, as provided by Vendor, infringes any U.S. patent, copyright, or trademark, and shall pay damages finally awarded or agreed in settlement. If the Service is enjoined, Vendor shall, at its option, procure the right for Customer to continue using it, modify it to be non-infringing, or terminate the subscription and refund prepaid unused fees.",
                    "9.2 Customer shall defend Vendor against third-party claims arising from Customer Data or Customer's use of the Service in violation of law or this Agreement, and shall pay damages finally awarded against Vendor."),
                new Section("10", ClauseTitles.LimitationOfLiability,
                    "10.1 EXCEPT FOR LIABILITY ARISING FROM SECTION 9 (INDEMNIFICATION) OR A PARTY'S GROSS NEGLIGENCE OR WILLFUL MISCONDUCT, EACH PARTY'S AGGREGATE LIABILITY ARISING OUT OF OR RELATING TO THIS AGREEMENT SHALL NOT EXCEED THE FEES PAID OR PAYABLE BY CUSTOMER FOR THE SERVICE IN THE SIX (6) MONTHS IMMEDIATELY PRECEDING THE FIRST EVENT GIVING RISE TO LIABILITY.",
                    "10.2 IN NO EVENT IS EITHER PARTY LIABLE FOR INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES, OR FOR LOSS OF PROFITS, REVENUE, OR DATA, HOWEVER CAUSED AND UNDER ANY THEORY OF LIABILITY."),
                new Section("11", "Publicity",
                    "Neither party may use the other party's name or logo in marketing materials without prior written consent, except that Vendor may identify Customer as a customer in factual lists of customers after Customer's written approval of the specific usage."),
                new Section("12", "Governing Law",
                    "This Agreement is governed by the laws of the State of California, without regard to conflict of laws principles. Any dispute shall be resolved exclusively in the state or federal courts located in San Francisco County, California, and each party consents to personal jurisdiction there."),
                new Section("13", "General Provisions",
                    "This Agreement, including order forms, is the entire agreement between the parties and supersedes all prior agreements regarding its subject matter. Amendments must be in writing and signed. Neither party may assign this Agreement without consent, except to an affiliate or successor in a merger or acquisition. Failure to enforce a provision is not a waiver. If any provision is unenforceable, it will be modified to the minimum extent necessary."),
            ]),
    ];
}

// Upload-test contracts written to /test-pdfs by --extra. Same fictional
// universe as the seed set, with contrasting terms (notice periods, caps,
// breach-notification windows, deletion timelines) for comparison questions.
static class ExtraTestContracts
{
    public static readonly ContractDoc[] All =
    [
        new ContractDoc(
            "Consulting-Agreement-Bluewater-Northgate.pdf",
            "CONSULTING SERVICES AGREEMENT",
            [
                "This Consulting Services Agreement (the \"Agreement\") is entered into as of April 20, 2026 (the \"Effective Date\") by and between Bluewater Analytics LLC, a Colorado limited liability company (\"Consultant\"), and Northgate Financial Group, Inc., a New York corporation (\"Company\").",
                "Company desires to engage Consultant to provide data analytics advisory services, and Consultant agrees to provide such services on the terms set forth below.",
            ],
            [
                new Section("1", "Engagement and Services",
                    "1.1 Company engages Consultant to provide the advisory and analytics services described in Exhibit A (the \"Services\"), including quarterly portfolio risk reviews and development of reporting dashboards.",
                    "1.2 Consultant shall determine the manner and means of performing the Services, shall meet the milestones in Exhibit A, and shall keep Company reasonably informed of progress at least twice per month."),
                new Section("2", "Term and Termination",
                    "2.1 This Agreement begins on the Effective Date and continues for one (1) year unless terminated earlier as provided below.",
                    "2.2 Either party may terminate this Agreement for convenience upon fifteen (15) days' prior written notice to the other party.",
                    "2.3 Either party may terminate immediately for material breach if the breach is not cured within ten (10) days after written notice describing the breach.",
                    "2.4 Upon termination, Company shall pay Consultant for all Services performed through the effective date of termination. Sections 5, 6, 7, 9, and 10 survive termination."),
                new Section("3", "Fees and Payment",
                    "3.1 Company shall pay Consultant a monthly retainer of forty thousand U.S. dollars (US $40,000), invoiced monthly in advance.",
                    "3.2 Invoices are due within fifteen (15) days of the invoice date (net 15). Late amounts accrue interest at one percent (1%) per month or the maximum lawful rate, whichever is less.",
                    "3.3 Pre-approved out-of-pocket expenses are reimbursable at cost with receipts; Consultant is responsible for its own taxes as an independent contractor."),
                new Section("4", "Independent Contractor",
                    "Consultant is an independent contractor, not an employee, agent, or partner of Company. Consultant has no authority to bind Company, is not eligible for Company benefits, and is solely responsible for employment taxes and withholdings for its personnel."),
                new Section("5", "Confidentiality",
                    "5.1 Each party shall protect the other party's non-public business, technical, and financial information with at least reasonable care, use it only to perform under this Agreement, and not disclose it to third parties except to personnel bound by equivalent obligations.",
                    "5.2 These confidentiality obligations survive for four (4) years after termination or expiration of this Agreement; obligations regarding trade secrets survive for as long as trade secret protection applies."),
                new Section("6", "Intellectual Property",
                    "6.1 All deliverables created by Consultant specifically for Company under this Agreement are works made for hire and are owned by Company upon full payment. To the extent any such deliverable does not qualify as a work made for hire, Consultant hereby assigns all right, title, and interest in it to Company.",
                    "6.2 Consultant retains ownership of its pre-existing methodologies, models, and tools, and grants Company a perpetual, non-exclusive, royalty-free license to use them solely as embedded in the deliverables."),
                new Section("7", "Non-Solicitation",
                    "During the term of this Agreement and for twelve (12) months thereafter, neither party shall directly solicit for employment any employee of the other party who was materially involved in the Services, except through general advertisements not targeted at such employees."),
                new Section("8", "Warranties",
                    "8.1 Consultant warrants that the Services will be performed in a professional and workmanlike manner consistent with industry standards and that deliverables will materially conform to Exhibit A for thirty (30) days following delivery.",
                    "8.2 Company's exclusive remedy for breach of the foregoing warranty is re-performance of the non-conforming Services. EXCEPT AS STATED ABOVE, CONSULTANT DISCLAIMS ALL OTHER WARRANTIES, EXPRESS OR IMPLIED."),
                new Section("9", ClauseTitles.Indemnification,
                    "Consultant shall defend and indemnify Company against third-party claims alleging that the deliverables, as provided by Consultant, infringe a U.S. copyright or trade secret, provided Company gives prompt written notice and reasonable cooperation. Each party shall indemnify the other for third-party claims of bodily injury or tangible property damage caused by its negligence."),
                new Section("10", ClauseTitles.LimitationOfLiability,
                    "EXCEPT FOR BREACH OF SECTION 5 (CONFIDENTIALITY), AMOUNTS PAYABLE UNDER SECTION 9 (INDEMNIFICATION), OR A PARTY'S GROSS NEGLIGENCE OR WILLFUL MISCONDUCT, EACH PARTY'S TOTAL AGGREGATE LIABILITY UNDER THIS AGREEMENT SHALL NOT EXCEED THE FEES PAID OR PAYABLE BY COMPANY IN THE THREE (3) MONTHS PRECEDING THE EVENT GIVING RISE TO THE CLAIM, AND NEITHER PARTY IS LIABLE FOR INDIRECT, INCIDENTAL, SPECIAL, OR CONSEQUENTIAL DAMAGES."),
                new Section("11", "Insurance",
                    "Consultant shall maintain during the term professional liability (errors and omissions) insurance of at least one million U.S. dollars (US $1,000,000) per claim and commercial general liability insurance of at least one million U.S. dollars (US $1,000,000) per occurrence, and shall provide certificates of insurance upon request."),
                new Section("12", "Governing Law",
                    "This Agreement is governed by the laws of the State of Colorado, without regard to conflict of laws principles. The state and federal courts located in Denver County, Colorado have exclusive jurisdiction over disputes arising under this Agreement."),
                new Section("13", "General",
                    "This Agreement, including Exhibit A, is the entire agreement of the parties regarding its subject matter. Amendments must be in a writing signed by both parties. Neither party may assign this Agreement without the other party's prior written consent. If any provision is held unenforceable, the remainder continues in full force."),
            ]),

        new ContractDoc(
            "DPA-Cloudhaven-Northgate.pdf",
            "DATA PROCESSING ADDENDUM",
            [
                "This Data Processing Addendum (the \"DPA\") is entered into as of February 1, 2026 by and between Cloudhaven Technologies, Inc. (\"Processor\") and Northgate Financial Group, Inc. (\"Controller\"), and supplements the Software as a Service Subscription Agreement between the parties dated February 1, 2026 (the \"Subscription Agreement\").",
                "This DPA governs Processor's processing of personal data contained in Customer Data on behalf of Controller in connection with the Service.",
            ],
            [
                new Section("1", "Definitions and Scope",
                    "1.1 \"Personal Data\" means any information relating to an identified or identifiable natural person contained in Customer Data. \"Processing\", \"Controller\", \"Processor\", and \"Data Subject\" have the meanings given under applicable data protection law.",
                    "1.2 This DPA applies to all Processing of Personal Data by Processor on behalf of Controller under the Subscription Agreement. In the event of a conflict between this DPA and the Subscription Agreement regarding Personal Data, this DPA controls."),
                new Section("2", "Roles and Processing Instructions",
                    "2.1 Controller is the controller and Processor is the processor of Personal Data under this DPA.",
                    "2.2 Processor shall process Personal Data only on documented instructions from Controller, including as set forth in the Subscription Agreement, unless required otherwise by applicable law, in which case Processor shall inform Controller of that legal requirement before processing unless the law prohibits such notice."),
                new Section("3", "Confidentiality of Processing Personnel",
                    "Processor shall ensure that all personnel authorized to process Personal Data are bound by written confidentiality obligations or are under an appropriate statutory obligation of confidentiality, and that access is limited to personnel who need it to perform the Service."),
                new Section("4", "Security Measures",
                    "4.1 Processor shall implement and maintain technical and organizational measures aligned with ISO/IEC 27001 and its SOC 2 Type II program, including encryption of Personal Data in transit (TLS 1.2 or higher) and at rest (AES-256), multi-factor authentication for administrative access, role-based access controls, logging and monitoring, and annual penetration testing.",
                    "4.2 Processor shall not materially decrease the overall security of the Service during a Subscription Term."),
                new Section("5", "Personal Data Breach Notification",
                    "Processor shall notify Controller without undue delay, and in any event within forty-eight (48) hours, after becoming aware of a breach of security leading to the accidental or unlawful destruction, loss, alteration, unauthorized disclosure of, or access to Personal Data. The notification shall describe the nature of the breach, the categories and approximate number of Data Subjects affected, the likely consequences, and the measures taken or proposed to address the breach."),
                new Section("6", "Subprocessors",
                    "6.1 Controller provides general authorization for Processor to engage subprocessors listed at Processor's trust portal. Processor shall give Controller at least thirty (30) days' prior written notice of any intended addition or replacement of a subprocessor.",
                    "6.2 Controller may object in writing on reasonable data protection grounds within the notice period; if the parties cannot resolve the objection, Controller may terminate the affected portion of the Service and receive a pro-rata refund of prepaid fees.",
                    "6.3 Processor shall impose data protection obligations on each subprocessor that are no less protective than those in this DPA and remains fully liable for its subprocessors' performance."),
                new Section("7", "Data Subject Requests",
                    "Taking into account the nature of the Processing, Processor shall assist Controller by appropriate technical and organizational measures in fulfilling Controller's obligations to respond to Data Subject requests (access, rectification, erasure, restriction, portability, objection). Processor shall forward to Controller within ten (10) business days any Data Subject request it receives directly and shall not respond to it except to direct the Data Subject to Controller."),
                new Section("8", "Audits",
                    "8.1 Processor shall make available to Controller information reasonably necessary to demonstrate compliance with this DPA, including its most recent SOC 2 Type II report and ISO/IEC 27001 certificate, which shall satisfy Controller's audit right where they reasonably address the scope of the requested audit.",
                    "8.2 Where reports are insufficient, Controller may conduct an audit no more than once in any twelve (12) month period, on at least thirty (30) days' written notice, during business hours, subject to Processor's reasonable security policies, and at Controller's expense."),
                new Section("9", "International Transfers",
                    "Processor shall not transfer Personal Data outside the country of origin except in compliance with applicable data protection law, including, where required, execution of the applicable standard contractual clauses or reliance on another lawful transfer mechanism. Processor's current hosting regions are listed in the Service documentation."),
                new Section("10", "Return and Deletion of Personal Data",
                    "Upon termination or expiration of the Subscription Agreement, Processor shall, at Controller's election, return Personal Data in a commonly used machine-readable format or delete it. Absent an election, Processor shall delete all Personal Data within sixty (60) days of termination, except for copies retained in routine encrypted backups, which shall be deleted in the ordinary course and in any event within one hundred eighty (180) days, and except as retention is required by law."),
                new Section("11", "Liability",
                    "Each party's liability arising out of or related to this DPA is subject to the limitations and exclusions of liability set forth in Section 10 (Limitation of Liability) of the Subscription Agreement, and liability under this DPA and the Subscription Agreement is aggregated, not cumulative."),
                new Section("12", "Term and Governing Law",
                    "This DPA is effective for as long as Processor processes Personal Data under the Subscription Agreement and terminates automatically upon completion of the deletion obligations in Section 10. This DPA is governed by the law governing the Subscription Agreement (the laws of the State of California), and disputes are subject to the same exclusive jurisdiction."),
            ]),
    ];
}
