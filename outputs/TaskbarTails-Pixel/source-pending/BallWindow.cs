using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
namespace TaskbarTails;
public sealed class BallWindow:Window
{
 IntPtr handle;
 public BallWindow(){Width=16;Height=16;WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;Topmost=true;ShowInTaskbar=false;ShowActivated=false;Content=new Ball();SourceInitialized+=(_,_)=>{handle=new WindowInteropHelper(this).Handle;Native.SetWindowLong(handle,-20,Native.GetWindowLong(handle,-20)|0x08000000|0x80|0x20);};}
 public void Place(double x,double y,double dpi){if(!IsVisible)Show();Native.SetWindowPos(handle,new IntPtr(-1),(int)x,(int)y,(int)(16*dpi),(int)(16*dpi),0x0010);}
 sealed class Ball:FrameworkElement{protected override void OnRender(DrawingContext d){string[] rows={"00111100","01111110","11111111","11111111","11111111","11111111","01111110","00111100"};for(int y=0;y<8;y++)for(int x=0;x<8;x++)if(rows[y][x]=='1')d.DrawRectangle(new SolidColorBrush((Color)ColorConverter.ConvertFromString(x<4&&y<3?"#FFE3A4":"#E89079")),null,new Rect(x*2,y*2,2,2));}}
}
