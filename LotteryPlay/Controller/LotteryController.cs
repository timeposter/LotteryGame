using LotteryPlay.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

[Route("api/[controller]")]
[ApiController]
public class LotteryController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public LotteryController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// 获取当前期号、倒计时、投注状态
    /// </summary>
    [HttpGet("GetCurrentPeriod")]
    public async Task<IActionResult> GetCurrentPeriod(int lotType)
    {
        var current = await _dbContext.LotteryDatas
            .Where(m => (int)m.LotteryId == lotType && m.IsOpen == 0)
            .OrderBy(m => m.PeriodNo)
            .FirstOrDefaultAsync();

        if (current == null)
        {
            return Ok(new { code = 0 });
        }

        TimeSpan left = current.EndTime - DateTime.Now;
        string countTime = left.TotalSeconds > 0
            ? $"{(int)left.TotalMinutes:00}:{left.Seconds:00}"
            : "00:00";
        bool canBet = left.TotalSeconds > 0;

        return Ok(new
        {
            code = 1,
            period = current.PeriodNo,
            countTime = countTime,
            canBet = canBet
        });
    }

    /// <summary>
    /// 获取历史开奖记录
    /// </summary>
    [HttpGet("GetLotteryHistory")]
    public async Task<IActionResult> GetLotteryHistory(int lotType)
    {
        var list = await _dbContext.LotteryDatas
            .Where(m => m.LotteryId == lotType && m.IsOpen == 1)
            .OrderByDescending(m => m.PeriodNo)
            .Take(10)
            .Select(m => new
            {
                period = m.PeriodNo,
                openNumber = m.OpenNumber,
                openTime = m.OpenTime.Value.ToString("HH:mm:ss")
            })
            .ToListAsync();

        return Ok(new
        {
            code = 1,
            data = list
        });
    }
}