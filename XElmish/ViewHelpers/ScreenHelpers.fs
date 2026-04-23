namespace OdisTimetableDownloaderMAUI

open Fabulous
open Fabulous.Maui

open Microsoft.Maui
open Microsoft.Maui.Controls
open Microsoft.Maui.Graphics

open type Fabulous.Maui.View

module Theme =

    let internal teal050 = Color.FromArgb("#FFE1F5EE")
    let internal teal100 = Color.FromArgb("#FF9FE1CB")
    let internal teal200 = Color.FromArgb("#FF5DCAA5")
    let internal teal400 = Color.FromArgb("#FF1D9E75")
    let internal teal600 = Color.FromArgb("#FF0F6E56")
    let internal teal800 = Color.FromArgb("#FF085041")

    let internal gray050 = Color.FromArgb("#FFF1EFE8")
    let internal gray100 = Color.FromArgb("#FFD3D1C7")
    let internal gray200 = Color.FromArgb("#FFB4B2A9")
    let internal gray400 = Color.FromArgb("#FF888780")
    let internal gray600 = Color.FromArgb("#FF5F5E5A")
    let internal gray800 = Color.FromArgb("#FF444441")

    let internal red050  = Color.FromArgb("#FFFCEBEB")
    let internal red100  = Color.FromArgb("#FFF7C1C1")
    let internal red200  = Color.FromArgb("#FFF09595")
    let internal red400  = Color.FromArgb("#FFE24B4A")
    let internal red600  = Color.FromArgb("#FFA32D2D")
    let internal red800  = Color.FromArgb("#FF791F1F")

    let internal amber050 = Color.FromArgb("#FFFAEEDA")
    let internal amber100 = Color.FromArgb("#FFFAC775")
    let internal amber400 = Color.FromArgb("#FFBA7517")
    let internal amber800 = Color.FromArgb("#FF633806")

    let internal blue050 = Color.FromArgb("#FFE6F1FB")
    let internal blue100 = Color.FromArgb("#FFB5D4F4")
    let internal blue400 = Color.FromArgb("#FF378ADD")
    let internal blue800 = Color.FromArgb("#FF0C447C")

    let internal pageBg      = Color.FromArgb("#FFF5F5F3")   // light surface behind cards
    let internal cardBg      = Colors.White
    let internal cardBorder  = Color.FromArgb("#1A000000")   // 10 % black  ≈ 0.5 px visual weight
    let internal textPrimary = Color.FromArgb("#FF1A1A18")
    let internal textSecond  = gray600
    let internal textHint    = gray400

module ScreenHelpers =

    open Theme

    let internal brush (c : Color) = SolidColorBrush(c)   
                                    
    let internal topBarBrush<'a>() : WidgetBuilder<'a, IFabSolidColorBrush> = brush teal400
    let internal cardBgBrush<'a>() : WidgetBuilder<'a, IFabSolidColorBrush> = brush cardBg 

    let internal pageBgBrush<'a>() : WidgetBuilder<'a, IFabSolidColorBrush> = brush pageBg

    let internal teal050Brush<'a>() : WidgetBuilder<'a, IFabSolidColorBrush> = brush teal050
    let internal teal100Brush<'a>() : WidgetBuilder<'a, IFabSolidColorBrush> = brush teal100
    let internal teal400Brush<'a>() : WidgetBuilder<'a, IFabSolidColorBrush> = brush teal400
    let internal teal600Brush<'a>() : WidgetBuilder<'a, IFabSolidColorBrush> = brush teal600

    let internal gray050Brush<'a>() : WidgetBuilder<'a, IFabSolidColorBrush>  = brush gray050
    let internal gray100Brush<'a>() : WidgetBuilder<'a, IFabSolidColorBrush>  = brush gray100
    let internal red050Brush<'a>() : WidgetBuilder<'a, IFabSolidColorBrush>   = brush red050
    let internal red100Brush<'a>() : WidgetBuilder<'a, IFabSolidColorBrush>   = brush red100
    let internal red400Brush<'a>() : WidgetBuilder<'a, IFabSolidColorBrush>   = brush red400
    let internal red600Brush<'a>() : WidgetBuilder<'a, IFabSolidColorBrush>   = brush red600
    let internal amber050Brush<'a>() : WidgetBuilder<'a, IFabSolidColorBrush> = brush amber050

    let internal blue050Brush<'a>() : WidgetBuilder<'a, IFabSolidColorBrush>  = brush blue050
    let internal cardBorderBrush<'a>() : WidgetBuilder<'a, IFabSolidColorBrush> = brush cardBorder

    // =============================================
    // VIEW HELPERS
    // =============================================

    /// Thin separator line between groups.
    let divider () =
        BoxView(color = gray100)
            .height(0.5)
            .margin(Thickness(0., 4., 0., 4.))

    /// Small all-caps section label above a group of cards.
    let sectionLabel text =
        Label(text)
            .font(size = 11.)
            .textColor(textHint)
            .margin(Thickness(0., 8., 0., 2.))

    /// Coloured square icon badge used inside card rows.
    let iconBadge (bg : Color) (fg : Color) (glyph : string) =
        Border(
            Label(glyph)
                .font(size = 16.)
                .textColor(fg)
                .centerTextHorizontal()
                .centerVertical()
        )
            .background(brush bg)
            .strokeShape(RoundRectangle(cornerRadius = 10.))
            .stroke(SolidColorBrush(Colors.Transparent))
            .strokeThickness(0.)
            .width(38.)
            .height(38.)

    /// One tappable download / action row (icon + title + hint + right arrow).
    let actionCard (icon : WidgetBuilder<'a, #IFabView>) (msg : 'a) label hint =
        Border(
            ContentView(
                HStack(spacing = 12.) {
                    icon
                    VStack(spacing = 2.) {
                        Label(label)
                            .font(size = 14.)
                            .textColor(textPrimary)
                        Label(hint)
                            .font(size = 12.)
                            .textColor(textSecond)
                    }
                    Label("›")
                        .font(size = 18.)
                        .textColor(textHint)
                        .centerVertical()
                        .horizontalOptions(LayoutOptions.End)
                }
            )
                .padding(Thickness(14., 12., 14., 12.))
        )
            .background(cardBgBrush<'a>())
            .stroke(cardBorderBrush<'a>())
            .strokeShape(RoundRectangle(cornerRadius = 12.))
            .strokeThickness(0.5)
            .margin(Thickness(0., 2., 0., 2.))
            |> fun b -> b.gestureRecognizers() { TapGestureRecognizer(msg) }

    let disabledCard (icon : WidgetBuilder<'a, #IFabView>) label pillText =
        Border(
            ContentView(
                HStack(spacing = 12.) {
                    icon
                    VStack(spacing = 2.) {
                        Label(label)
                            .font(size = 14.)
                            .textColor(textPrimary)
                        Label(pillText)
                            .font(size = 12.)
                            .textColor(textSecond)
                    }                   
                }
            )
                .padding(Thickness(14., 12., 14., 12.))
        )
            .background(gray050Brush<'a>())
            .stroke(cardBorderBrush<'a>())
            .strokeShape(RoundRectangle(cornerRadius = 12.))
            .strokeThickness(0.5)
            .margin(Thickness(0., 2., 0., 2.))

    /// Disabled action row – no tap handler, grayed out with a status pill.
    let disabledCard2 (icon : WidgetBuilder<'a, #IFabView>) label pillText =
        Border(
            ContentView(
                HStack(spacing = 12.) {
                    icon
                    VStack(spacing = 2.) {
                        Label(label)
                            .font(size = 14.)
                            .textColor(gray400)
                    }
                    Border(
                        Label(pillText)
                            .font(size = 11.)
                            .textColor(amber800)
                            .centerTextHorizontal()
                            .centerVertical()
                    )
                        .background(amber050Brush<'a>())
                        .strokeShape(RoundRectangle(cornerRadius = 20.))
                        .stroke(SolidColorBrush(Colors.Transparent))
                        .strokeThickness(0.)
                        .padding(Thickness(8., 3., 8., 3.))
                        .horizontalOptions(LayoutOptions.End)
                }
            )
                .padding(Thickness(14., 12., 14., 12.))
        )
            .background(gray050Brush<'a>())
            .stroke(cardBorderBrush<'a>())
            .strokeShape(RoundRectangle(cornerRadius = 12.))
            .strokeThickness(0.5)
            .margin(Thickness(0., 2., 0., 2.))

    /// Top bar shared by all screens.
    let topBar connText title subtitle =   

        let titleLabel =
            Label(title)
                .font(size = 18.)
                .textColor(Colors.White)
    
        let subtitleLabel =
            Label(subtitle)
                .font(size = 12.)
                .textColor(Color.FromArgb("#D0FFFFFF"))
    
        let dot =
            BoxView(color = teal100)
                .width(7.)
                .height(7.)
                .cornerRadius(3.5)
                .centerVertical()
    
        let connLabel =
            Label(connText)
                .font(size = 11.)
                .textColor(Colors.White)
                .centerVertical()
    
        let connPill =
            Border(
                HStack(spacing = 6.) {
                    dot
                    connLabel

                }
            )
                .background(brush (Color.FromArgb("#30FFFFFF")))
                .strokeShape(RoundRectangle(cornerRadius = 20.))
                .stroke(SolidColorBrush(Colors.Transparent))
                .strokeThickness(0.)
                .padding(Thickness(10., 3., 10., 3.))
    
        (VStack(spacing = 4.) {
            titleLabel
            subtitleLabel
            connPill
        })
            .padding(Thickness(20., 20., 20., 16.))
            .background(topBarBrush<'a>())

    let resultCircle (bg : Color) glyph =
        Border(
            Label(glyph)
                .font(size = 28.)
                .centerTextHorizontal()
                .centerVertical()
        )
            .background(brush bg)
            .strokeShape(RoundRectangle(cornerRadius = 30.))
            .stroke(SolidColorBrush(Colors.Transparent))
            .strokeThickness(0.)
            .width(60.)
            .height(60.)