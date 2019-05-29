using Autodesk.Maya.OpenMaya;
using Autodesk.Maya.OpenMayaUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CODTools
{
    internal class MayaSceneConfigure : IDisposable
    {
        private string LinearUnit { get; set; }
        private string AngleUnit { get; set; }
        private bool CanProgress { get; set; }

        public int SceneStart { get; set; }
        public int SceneEnd { get; set; }

        public MayaSceneConfigure()
        {
            var _Linear = string.Empty;
            var _Angle = string.Empty;
            var _SceneStart = 0.0;
            var _SceneEnd = 0.0;

            // Fetch
            MGlobal.executeCommand("currentUnit -q -fullName -linear", out _Linear);
            MGlobal.executeCommand("currentUnit -q -fullName -angle", out _Angle);
            MGlobal.executeCommand("playbackOptions -q -ast", out _SceneStart);
            MGlobal.executeCommand("playbackOptions -q -aet", out _SceneEnd);

            // Parse start and end
            this.SceneStart = (int)(_SceneStart);
            this.SceneEnd = (int)(_SceneEnd);

            // Set
            this.LinearUnit = _Linear;
            this.AngleUnit = _Angle;

            // Change to CODTools formats
            MGlobal.executeCommand("currentUnit -linear cm");
            MGlobal.executeCommand("currentUnit -angle deg");

            // Progress
            this.CanProgress = true;
        }

        public void StartProgress(string Title, int Max)
        {
            if (this.CanProgress)
            {
                MGlobal.executeCommand("progressBar -e -bp -ii 1 $gMainProgressBar");
                MGlobal.executeCommand("progressBar -e -ep $gMainProgressBar");
                MGlobal.executeCommand(string.Format("progressBar -e -bp -ii 1 -st \"{0}\" -max {1} $gMainProgressBar", Title, Max));
            }
        }

        public void StepProgress()
        {
            MGlobal.executeCommand("progressBar -e -s 1 $gMainProgressBar");
        }

        public void SetTime(int Frame)
        {
            MGlobal.executeCommand(string.Format("currentTime {0}", Frame));
        }

        public void Dispose()
        {
            // Reset config
            MGlobal.executeCommand(string.Format("currentUnit -linear {0}", this.LinearUnit));
            MGlobal.executeCommand(string.Format("currentUnit -angle {0}", this.AngleUnit));

            // End
            if (this.CanProgress)
                MGlobal.executeCommand("progressBar -e -ep $gMainProgressBar");
        }
    }
}
