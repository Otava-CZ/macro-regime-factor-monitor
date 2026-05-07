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
}
