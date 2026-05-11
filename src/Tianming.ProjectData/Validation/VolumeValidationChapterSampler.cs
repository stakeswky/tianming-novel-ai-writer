using System;
using System.Collections.Generic;
using System.Linq;
using TM.Services.Modules.ProjectData.Models.Generated;

namespace TM.Services.Modules.ProjectData.Implementations
{
    public static class VolumeValidationChapterSampler
    {
        public static int CalculateSampleCount(int totalCount)
        {
            var sample = (int)Math.Ceiling(totalCount / 5.0);
            return Math.Max(3, Math.Min(50, sample));
        }

        public static List<ChapterInfo> SampleChapters(IEnumerable<ChapterInfo> chapters, int maxCount)
        {
            var chapterList = chapters.ToList();
            if (chapterList.Count == 0 || maxCount <= 0)
                return new List<ChapterInfo>();

            if (chapterList.Count <= maxCount)
                return chapterList.ToList();

            if (maxCount == 1)
                return new List<ChapterInfo> { chapterList[0] };

            var sampled = new List<ChapterInfo>();
            var totalCount = chapterList.Count;
            var step = (double)(totalCount - 1) / (maxCount - 1);

            for (var i = 0; i < maxCount; i++)
            {
                var index = (int)Math.Round(i * step);
                index = Math.Min(index, totalCount - 1);

                if (!sampled.Contains(chapterList[index]))
                {
                    sampled.Add(chapterList[index]);
                }
            }

            if (!sampled.Contains(chapterList[0]))
            {
                sampled.Insert(0, chapterList[0]);
                if (sampled.Count > maxCount)
                    sampled.RemoveAt(sampled.Count - 1);
            }

            if (!sampled.Contains(chapterList[totalCount - 1]))
            {
                if (sampled.Count >= maxCount)
                    sampled.RemoveAt(sampled.Count - 1);
                sampled.Add(chapterList[totalCount - 1]);
            }

            return sampled.OrderBy(chapter => chapter.ChapterNumber).ToList();
        }
    }
}
