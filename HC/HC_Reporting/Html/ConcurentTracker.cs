// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;

namespace VeeamHealthCheck.Html
{
    class ConcurentTracker
    {
        public DayOfWeek DayofTheWeeek { get; set; }
        public DateTime Date { get; set; }
        public int Hour { get; set; }
        public int hourMinute { get; set; }
        public int Minutes { get; set; }
        public TimeSpan Duration { get; set; }
        public ConcurentTracker()
        {

        }
    }

}
