﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Vixen.Common {
	interface ITimed {
		long StartTime { get; }
		long EndTime { get; }
		bool IsEmpty { get; }
	}
}
