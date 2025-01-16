using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_control_software_WPF.view_models.logging
{
    public interface IFlushableLogger
    {
        /// <summary>
        /// Force this logger to write all accumulated logs to file.
        /// </summary>
        void Flush();
    }
}
