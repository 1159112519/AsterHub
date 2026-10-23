using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Serialization;
using Microsoft.Win32;
using Forms=System.Windows.Forms;

public sealed class TrayColors : Forms.ProfessionalColorTable {
 public static System.Drawing.Color Convert(SolidColorBrush b){return System.Drawing.Color.FromArgb(b.Color.R,b.Color.G,b.Color.B);}
 public override System.Drawing.Color ToolStripDropDownBackground{get{return Convert(Theme.Surface);}}public override System.Drawing.Color ImageMarginGradientBegin{get{return ToolStripDropDownBackground;}}public override System.Drawing.Color ImageMarginGradientMiddle{get{return ToolStripDropDownBackground;}}public override System.Drawing.Color ImageMarginGradientEnd{get{return ToolStripDropDownBackground;}}public override System.Drawing.Color MenuItemSelected{get{return Convert(Theme.Raised);}}public override System.Drawing.Color MenuItemBorder{get{return Convert(Theme.Teal);}}public override System.Drawing.Color MenuBorder{get{return Convert(Theme.Line);}}public override System.Drawing.Color CheckBackground{get{return MenuItemSelected;}}public override System.Drawing.Color CheckSelectedBackground{get{return MenuItemSelected;}}
}
