// Global using aliases to resolve ambiguities between WPF and WinForms types
// (caused by <UseWindowsForms>true</UseWindowsForms> in .csproj)

// WPF (preferred for our UI)
global using Application = System.Windows.Application;
global using UserControl = System.Windows.Controls.UserControl;
global using Window = System.Windows.Window;
global using ComboBox = System.Windows.Controls.ComboBox;
global using CheckBox = System.Windows.Controls.CheckBox;
global using Button = System.Windows.Controls.Button;
global using TextBox = System.Windows.Controls.TextBox;
global using MessageBox = System.Windows.MessageBox;
global using MessageBoxImage = System.Windows.MessageBoxImage;
global using MessageBoxButton = System.Windows.MessageBoxButton;
global using MessageBoxResult = System.Windows.MessageBoxResult;
global using FlowDirection = System.Windows.FlowDirection;
global using FrameworkElement = System.Windows.FrameworkElement;
global using ResourceDictionary = System.Windows.ResourceDictionary;
