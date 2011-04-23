﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vixen.Sys;

namespace Vixen.Script {
	interface IScriptSkeletonGenerator {
		ScriptSequence Sequence { get; set; }
		string TransformText();
	}
}
