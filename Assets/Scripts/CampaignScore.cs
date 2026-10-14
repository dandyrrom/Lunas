using System.Collections.Generic;

/// <summary>
/// Scores kept for the run, from a splash "start" through the ending scoreboard.
/// Scene loads do not clear it. A new game from the splash menu does.
/// </summary>
public static class CampaignScore
{
    public struct Line
    {
        public string levelName;
        public int killed;
        public int total;
        public int missedThrows;
        public int livesLeft;
        public int score;
    }

    static readonly List<Line> lines = new List<Line>(4);

    public static IReadOnlyList<Line> Lines => lines;

    public static int Total
    {
        get
        {
            int sum = 0;
            for (int i = 0; i < lines.Count; i++)
                sum += lines[i].score;
            return sum;
        }
    }

    public static void Reset()
    {
        lines.Clear();
    }

    public static void Record(
        string levelName,
        int killed,
        int total,
        int missedThrows,
        int livesLeft,
        int score)
    {
        if (string.IsNullOrWhiteSpace(levelName))
            return;

        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].levelName != levelName)
                continue;

            lines[i] = Make(levelName, killed, total, missedThrows, livesLeft, score);
            return;
        }

        lines.Add(Make(levelName, killed, total, missedThrows, livesLeft, score));
    }

    static Line Make(
        string levelName,
        int killed,
        int total,
        int missedThrows,
        int livesLeft,
        int score)
    {
        return new Line
        {
            levelName = levelName,
            killed = killed,
            total = total,
            missedThrows = missedThrows,
            livesLeft = livesLeft,
            score = score
        };
    }
}
