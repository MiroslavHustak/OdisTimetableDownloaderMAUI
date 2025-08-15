namespace OdisTimetableDownloaderMAUI

open Fabulous.Maui

module ProgressCircle =

    open System
    open Microsoft.Maui.Graphics

    open Types.Haskell_IO_Monad_Simulation

    //Anonymous Object Creation 
    let internal progressCircle (progress : float) =

        IO (fun () 
                ->
                { 
                    new IDrawable with

                        member _.Draw(canvas : ICanvas, dirtyRect : RectF) =

                            let centerX = dirtyRect.Width / 2f
                            let centerY = dirtyRect.Height / 2f
                            let radius = Math.Min(dirtyRect.Width, dirtyRect.Height) / 2f - 10f
                            let strokeWidth = 15f                            
    
                            let percentageText = sprintf "%.0f%%" (progress * 100.0)                        
                        
                            // Draw Background Circle
                            canvas.StrokeColor <- Colors.LightCyan
                            canvas.StrokeSize <- strokeWidth
                            canvas.DrawCircle(centerX, centerY, radius)
    
                            // Draw Progress Circle
                            let sweepAngle = progress * 359.9
                            canvas.StrokeColor <- Colors.Blue
                            canvas.DrawArc(centerX - radius, centerY - radius, radius * 2f, radius * 2f, -0f, float32 sweepAngle, false, false)

                            //procenta uvnitr progress circle
                    
                            // Measure the text
                            let textBounds = canvas.GetStringSize(percentageText, Font.Default, 24.0f)
    
                            // Calculate the position to center the text
                            let textX = centerX - textBounds.Width / 2.0f
                            //let textY = centerY - textBounds.Height / 2.0f
                            let textY = centerY - textBounds.Height / 2.0f + textBounds.Height / 2.0f
    
                            // Draw Percentage Text
                            canvas.FillColor <- Colors.LightCyan
                            canvas.FontSize <- 24.0f
                            canvas.DrawString(percentageText, centerX, centerY, HorizontalAlignment.Center)
                }
        )