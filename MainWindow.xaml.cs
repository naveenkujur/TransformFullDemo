using System.Windows;
using System.Windows.Media;

namespace transform;

/// <summary>Interaction logic for MainWindow.xaml</summary>
public partial class MainWindow : Window {
   public MainWindow () {
      InitializeComponent ();

      mDwgSurface.MouseLeftButtonDown += (sender, e) => DrawingClicked (mInvProjXfm.Transform (e.GetPosition (mDwgSurface)));
      mDwgSurface.MouseMove += (sender, e) => DrawingHover (mInvProjXfm.Transform (e.GetPosition (mDwgSurface)));
      mPanWidget = new PanWidget (mDwgSurface, OnPan);

      mDwgSurface.MouseMove += (sender, e) => {
         var ptCanvas = e.GetPosition (mDwgSurface);
         var ptDrawing = mInvProjXfm.Transform (ptCanvas);
         Title = $"Mouse: {ptCanvas.X:F2}, {ptCanvas.Y:F2} => {ptDrawing.X:F2}, {ptDrawing.Y:F2}";
      };

      mDwgSurface.MouseRightButtonDown += delegate { // Carry out zoom extents, on right mouse down!
         mProjXfm = Util.ComputeZoomExtentsProjXfm (mDwgSurface.ActualWidth, mDwgSurface.ActualHeight, mDrawing.Bound);
         mInvProjXfm = mProjXfm; mInvProjXfm.Invert ();
         mDwgSurface.Xfm = mProjXfm;
         mDwgSurface.InvalidateVisual ();
      };

      Loaded += delegate {
         var bound = new Bound (new Point (-10, -10), new Point (1000, 1000));
         mProjXfm = Util.ComputeZoomExtentsProjXfm (mDwgSurface.ActualWidth, mDwgSurface.ActualHeight, bound);
         mInvProjXfm = mProjXfm; mInvProjXfm.Invert ();
         mDwgSurface.Drawing = mDrawing;
         mDwgSurface.LineBuilder = mLineBuilder;
         mDwgSurface.Xfm = mProjXfm;
         mDrawing.RedrawReq += () => mDwgSurface.InvalidateVisual ();
         mLineBuilder.RedrawReq += () => mDwgSurface.InvalidateVisual ();
      };

      MouseWheel += (sender, e) => {
         double zoomFactor = 1.05;
         if (e.Delta > 0) zoomFactor = 1 / zoomFactor;
         var ptDraw = mInvProjXfm.Transform (e.GetPosition (mDwgSurface)); // mouse point in drawing space
         // Actual visible drawing area
         Point cornerA = mInvProjXfm.Transform (new Point ()), cornerB = mInvProjXfm.Transform (new Point (mDwgSurface.ActualWidth, mDwgSurface.ActualHeight));
         var b = new Bound (cornerA, cornerB);
         b = b.Inflated (ptDraw, zoomFactor);
         mProjXfm = Util.ComputeZoomExtentsProjXfm (mDwgSurface.ActualWidth, mDwgSurface.ActualHeight, b);
         mInvProjXfm = mProjXfm; mInvProjXfm.Invert ();
         mDwgSurface.Xfm = mProjXfm;
         mDwgSurface.InvalidateVisual ();
      };
   }

   #region Implementation
   /// <summary>Mouse click, in drawing space</summary>
   void DrawingClicked (Point drawingPt) {
      var pline = mLineBuilder.PointClicked (drawingPt);
      if (pline == null) return;
      mDrawing.AddPline (pline);
   }

   /// <summary>Mouse hover, in drawing space</summary>
   void DrawingHover (Point drawingPt) => mLineBuilder.PointHover (drawingPt);

   void OnPan (Vector panDisp) {
      Matrix m = Matrix.Identity; m.Translate (panDisp.X, panDisp.Y);
      mProjXfm.Append (m);
      mInvProjXfm = mProjXfm; mInvProjXfm.Invert ();
      mDwgSurface.Xfm = mProjXfm;
      mDwgSurface.InvalidateVisual ();
   }
   #endregion

   #region Private
   Matrix mProjXfm = Matrix.Identity, mInvProjXfm = Matrix.Identity;
   readonly Drawing mDrawing = new Drawing ();
   readonly LineBuilder mLineBuilder = new LineBuilder ();
   readonly PanWidget mPanWidget;
   #endregion
}
