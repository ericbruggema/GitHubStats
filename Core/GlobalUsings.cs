// Explicit global usings (instead of ImplicitUsings) so the Windows tray build, which adds
// System.Windows.Forms / System.Drawing, cannot introduce ambiguous type names elsewhere.
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;
