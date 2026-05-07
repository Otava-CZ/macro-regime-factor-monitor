using MacroRegimeFactorMonitor.Data;
using MacroRegimeFactorMonitor.Domain;
using Microsoft.EntityFrameworkCore;

namespace MacroRegimeFactorMonitor.Services;

public sealed class JournalService(IDbContextFactory<MacroRegimeDbContext> dbFactory)
{
    public async Task<List<WeeklyReview>> GetWeeklyReviewsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.WeeklyReviews
            .AsNoTracking()
            .OrderByDescending(review => review.WeekEnding)
            .ToListAsync();
    }

    public async Task AddWeeklyReviewAsync(WeeklyReview review)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        review.CreatedAtUtc = DateTime.UtcNow;
        db.WeeklyReviews.Add(review);
        await db.SaveChangesAsync();
    }

    public async Task<List<TradeIdea>> GetTradeIdeasAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.TradeIdeas
            .AsNoTracking()
            .OrderByDescending(idea => idea.IdeaDate)
            .ThenByDescending(idea => idea.CreatedAtUtc)
            .ToListAsync();
    }

    public async Task AddTradeIdeaAsync(TradeIdea idea)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        idea.CreatedAtUtc = DateTime.UtcNow;
        db.TradeIdeas.Add(idea);
        await db.SaveChangesAsync();
    }

    public async Task UpdateTradeIdeaAsync(TradeIdea idea)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var existingIdea = await db.TradeIdeas.SingleAsync(savedIdea => savedIdea.Id == idea.Id);

        existingIdea.IdeaDate = idea.IdeaDate;
        existingIdea.Title = idea.Title;
        existingIdea.Thesis = idea.Thesis;
        existingIdea.Instrument = idea.Instrument;
        existingIdea.Status = idea.Status;
        existingIdea.RiskNotes = idea.RiskNotes;
        existingIdea.EntryTrigger = idea.EntryTrigger;
        existingIdea.Invalidation = idea.Invalidation;
        existingIdea.Catalyst = idea.Catalyst;
        existingIdea.MaxLoss = idea.MaxLoss;
        existingIdea.TimeHorizon = idea.TimeHorizon;
        existingIdea.PostMortem = idea.PostMortem;

        await db.SaveChangesAsync();
    }
}
