using ContractClause.Shared;

namespace ContractClause.Tests;

public class ClauseChunkerTests
{
    private static readonly int[] ExpectedPageNumbers = [1, 2, 5];

    [Fact]
    public void Splits_on_simple_numbered_headings()
    {
        var pages = new[] { new PageText(1, """
            1. Definitions
            "Agreement" means this contract.
            2. Term
            This Agreement lasts two years.
            """) };

        var chunks = ClauseChunker.Chunk(pages);

        Assert.Equal(2, chunks.Count);
        Assert.Equal("1", chunks[0].ClauseNumber);
        Assert.Equal("Definitions", chunks[0].ClauseTitle);
        Assert.Contains("means this contract", chunks[0].Text);
        Assert.Equal("2", chunks[1].ClauseNumber);
        Assert.Equal("Term", chunks[1].ClauseTitle);
    }

    [Fact]
    public void Handles_nested_numbering_three_levels()
    {
        var pages = new[] { new PageText(1, """
            1. Payment
            General payment terms.
            1.1 Invoicing
            Invoices monthly.
            1.1.1 Late Fees
            Two percent per month.
            """) };

        var chunks = ClauseChunker.Chunk(pages);

        Assert.Equal(3, chunks.Count);
        Assert.Equal("1.1", chunks[1].ClauseNumber);
        Assert.Equal("1.1.1", chunks[2].ClauseNumber);
        Assert.Equal("Late Fees", chunks[2].ClauseTitle);
    }

    [Fact]
    public void Handles_section_and_article_with_roman_numerals()
    {
        var pages = new[] { new PageText(1, """
            Section 5 - Termination
            Either party may terminate.
            ARTICLE IV: Confidentiality
            Keep it secret.
            """) };

        var chunks = ClauseChunker.Chunk(pages);

        Assert.Equal(2, chunks.Count);
        Assert.Equal("5", chunks[0].ClauseNumber);
        Assert.Equal("Termination", chunks[0].ClauseTitle);
        Assert.Equal("IV", chunks[1].ClauseNumber);
        Assert.Equal("Confidentiality", chunks[1].ClauseTitle);
    }

    [Fact]
    public void Handles_all_caps_headings()
    {
        var pages = new[] { new PageText(1, """
            LIMITATION OF LIABILITY
            Liability is capped at fees paid.
            """) };

        var chunks = ClauseChunker.Chunk(pages);

        Assert.Single(chunks);
        Assert.Equal("Limitation Of Liability", chunks[0].ClauseTitle);
        Assert.Contains("capped", chunks[0].Text);
    }

    [Fact]
    public void Heading_split_across_pages_uses_heading_page_number()
    {
        // clause number is the last line of page 2; its title is the first line of page 3
        var pages = new[]
        {
            new PageText(2, """
                7. Indemnification
                Customer shall indemnify.
                7.1
                """),
            new PageText(3, """
                Defense of Claims
                Vendor will defend any claim.
                """)
        };

        var chunks = ClauseChunker.Chunk(pages);

        Assert.Equal(2, chunks.Count);
        Assert.Equal("7.1", chunks[1].ClauseNumber);
        Assert.Equal("Defense of Claims", chunks[1].ClauseTitle);
        Assert.Equal(2, chunks[1].PageNumber);   // heading started on page 2
        Assert.Contains("defend any claim", chunks[1].Text);
    }

    [Fact]
    public void Body_text_starting_with_numbers_is_not_a_heading()
    {
        var pages = new[] { new PageText(1, """
            4. Payment Terms
            Net 30 days from invoice date. If unpaid after
            60 days interest accrues at the statutory rate which shall be computed daily and compounded monthly without exception.
            """) };

        var chunks = ClauseChunker.Chunk(pages);

        Assert.Single(chunks);
        Assert.Contains("60 days interest", chunks[0].Text);
    }

    [Fact]
    public void Text_before_first_heading_becomes_preamble()
    {
        var pages = new[] { new PageText(1, """
            This Agreement is entered into by Acme Corp and Beta LLC.
            1. Definitions
            Words mean things.
            """) };

        var chunks = ClauseChunker.Chunk(pages);

        Assert.Equal(2, chunks.Count);
        Assert.Equal("Preamble", chunks[0].ClauseTitle);
        Assert.Equal("", chunks[0].ClauseNumber);
        Assert.Contains("Acme Corp", chunks[0].Text);
    }

    [Fact]
    public void Chunks_carry_correct_page_numbers()
    {
        var pages = new[]
        {
            new PageText(1, "1. First\nAlpha."),
            new PageText(2, "2. Second\nBravo."),
            new PageText(5, "3. Third\nCharlie.")
        };

        var chunks = ClauseChunker.Chunk(pages);

        Assert.Equal(ExpectedPageNumbers, chunks.Select(c => c.PageNumber).ToArray());
    }
}
