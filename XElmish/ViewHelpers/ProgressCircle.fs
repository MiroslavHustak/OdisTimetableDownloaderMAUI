namespace OdisTimetableDownloaderMAUI

open System
open Microsoft.Maui.Graphics

open Theme
open Types.Haskell_IO_Monad_Simulation

module ProgressCircle =

    //Anonymous Object Creation  //object expressions ({ new ... with ... }) are the F# way to implement OOP interfaces anonymously.
     
    let internal progressCircle (progress : float) =
 
        IO (fun () ->
 
             { new IDrawable with
 
                 member _.Draw(canvas : ICanvas, dirtyRect : RectF) =
 
                     let centerX = dirtyRect.Width / 2f
                     let centerY = dirtyRect.Height / 2f
 
                     let radius = Math.Min(dirtyRect.Width, dirtyRect.Height) / 2f - 10f
 
                     let strokeWidth = 15f
 
                     let percentageText = sprintf "%.0f%%" (progress * 100.0)
 
                     // Track circle
                     canvas.StrokeColor <- teal050
                     canvas.StrokeSize <- strokeWidth
 
                     canvas.DrawCircle(
                         centerX,
                         centerY,
                         radius
                     )
 
                     // Progress arc
                     let sweepAngle = progress * 359.9
 
                     canvas.StrokeColor <- teal400
 
                     canvas.DrawArc(
                         centerX - radius,
                         centerY - radius,
                         radius * 2f,
                         radius * 2f,
                         0f,
                         float32 sweepAngle,
                         false,
                         false
                     )
 
                     // Percentage text
                     canvas.FillColor <- textPrimary
                     canvas.FontSize <- 24.0f
 
                     canvas.DrawString(
                         percentageText,
                         dirtyRect,
                         HorizontalAlignment.Center,
                         VerticalAlignment.Center
                     )
             }
         )