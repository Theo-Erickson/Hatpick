namespace Hatpick
{
    // Control looks (templates). Colors come from the theme brushes by name, so these
    // never change between themes. Loaded once at startup with XamlReader.
    static class Styles
    {
        public const string Xaml = @"
<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                    xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>

  <!-- Standard small button -->
  <Style x:Key='Btn' TargetType='Button'>
    <Setter Property='Foreground' Value='{DynamicResource Text}'/>
    <Setter Property='Background' Value='{DynamicResource Panel}'/>
    <Setter Property='BorderBrush' Value='{DynamicResource Border}'/>
    <Setter Property='BorderThickness' Value='1'/>
    <Setter Property='Padding' Value='11,0'/>
    <Setter Property='Height' Value='30'/>
    <Setter Property='FontSize' Value='13'/>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='HorizontalContentAlignment' Value='Center'/>
    <Setter Property='VerticalContentAlignment' Value='Center'/>
    <Setter Property='FocusVisualStyle' Value='{x:Null}'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='Button'>
          <Border x:Name='b' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}'
                  BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='7' Padding='{TemplateBinding Padding}'
                  SnapsToDevicePixels='True'>
            <ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}'
                              VerticalAlignment='{TemplateBinding VerticalContentAlignment}'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Opacity' Value='0.86'/></Trigger>
            <Trigger Property='IsPressed' Value='True'><Setter TargetName='b' Property='Opacity' Value='0.72'/></Trigger>
            <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='b' Property='BorderBrush' Value='{DynamicResource Accent}'/></Trigger>
            <Trigger Property='IsEnabled' Value='False'><Setter TargetName='b' Property='Opacity' Value='0.45'/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key='BtnPrimary' TargetType='Button' BasedOn='{StaticResource Btn}'>
    <Setter Property='Background' Value='{DynamicResource Accent}'/>
    <Setter Property='Foreground' Value='{DynamicResource AccentText}'/>
    <Setter Property='BorderThickness' Value='0'/>
    <Setter Property='FontWeight' Value='SemiBold'/>
  </Style>

  <Style x:Key='BtnGhost' TargetType='Button' BasedOn='{StaticResource Btn}'>
    <Setter Property='Background' Value='Transparent'/>
    <Setter Property='BorderBrush' Value='Transparent'/>
  </Style>

  <Style x:Key='BtnLink' TargetType='Button' BasedOn='{StaticResource Btn}'>
    <Setter Property='Background' Value='Transparent'/>
    <Setter Property='BorderThickness' Value='0'/>
    <Setter Property='Foreground' Value='{DynamicResource Link}'/>
    <Setter Property='Padding' Value='4,0'/>
  </Style>

  <!-- Square icon button on the side rail -->
  <Style x:Key='RailBtn' TargetType='Button'>
    <Setter Property='Width' Value='44'/>
    <Setter Property='Height' Value='44'/>
    <Setter Property='Margin' Value='0,2'/>
    <Setter Property='Background' Value='Transparent'/>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='FocusVisualStyle' Value='{x:Null}'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='Button'>
          <Border x:Name='b' Background='{TemplateBinding Background}' CornerRadius='10'>
            <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Background' Value='{DynamicResource RailActive}'/></Trigger>
            <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='b' Property='Background' Value='{DynamicResource RailActive}'/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- Clickable card (repo cards, hat cards) -->
  <Style x:Key='Card' TargetType='Button'>
    <Setter Property='Background' Value='{DynamicResource Panel}'/>
    <Setter Property='BorderBrush' Value='{DynamicResource Border}'/>
    <Setter Property='BorderThickness' Value='1'/>
    <Setter Property='Foreground' Value='{DynamicResource Text}'/>
    <Setter Property='Padding' Value='14'/>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='HorizontalContentAlignment' Value='Stretch'/>
    <Setter Property='VerticalContentAlignment' Value='Stretch'/>
    <Setter Property='FocusVisualStyle' Value='{x:Null}'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='Button'>
          <Border x:Name='b' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}'
                  BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='12' Padding='{TemplateBinding Padding}'>
            <ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}'
                              VerticalAlignment='{TemplateBinding VerticalContentAlignment}'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Background' Value='{DynamicResource PanelHover}'/></Trigger>
            <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='b' Property='BorderBrush' Value='{DynamicResource Accent}'/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- Row in a list (repo list, menus) -->
  <Style x:Key='Row' TargetType='Button'>
    <Setter Property='Background' Value='Transparent'/>
    <Setter Property='Foreground' Value='{DynamicResource Text}'/>
    <Setter Property='Padding' Value='8,0'/>
    <Setter Property='MinHeight' Value='34'/>
    <Setter Property='FontSize' Value='13'/>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='HorizontalContentAlignment' Value='Stretch'/>
    <Setter Property='FocusVisualStyle' Value='{x:Null}'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='Button'>
          <Border x:Name='b' Background='{TemplateBinding Background}' CornerRadius='7' Padding='{TemplateBinding Padding}'>
            <ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='Center'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Background' Value='{DynamicResource PanelHover}'/></Trigger>
            <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='b' Property='Background' Value='{DynamicResource SelRow}'/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- Round ? button -->
  <Style x:Key='HelpBtn' TargetType='Button'>
    <Setter Property='Width' Value='18'/>
    <Setter Property='Height' Value='18'/>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='ToolTip' Value='What is this?'/>
    <Setter Property='FocusVisualStyle' Value='{x:Null}'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='Button'>
          <Border x:Name='b' CornerRadius='9' BorderThickness='1' BorderBrush='{DynamicResource Dashed}' Background='Transparent'>
            <TextBlock Text='?' FontSize='11' FontWeight='SemiBold' Foreground='{DynamicResource Muted}'
                       HorizontalAlignment='Center' VerticalAlignment='Center'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='BorderBrush' Value='{DynamicResource Accent}'/></Trigger>
            <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='b' Property='BorderBrush' Value='{DynamicResource Accent}'/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType='TextBox'>
    <Setter Property='Foreground' Value='{DynamicResource Text}'/>
    <Setter Property='Background' Value='{DynamicResource Input}'/>
    <Setter Property='BorderBrush' Value='{DynamicResource Border}'/>
    <Setter Property='CaretBrush' Value='{DynamicResource Text}'/>
    <Setter Property='SelectionBrush' Value='{DynamicResource Accent}'/>
    <Setter Property='BorderThickness' Value='1'/>
    <Setter Property='Padding' Value='8,5'/>
    <Setter Property='FontSize' Value='13'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='TextBox'>
          <Border x:Name='b' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}'
                  BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='7'>
            <ScrollViewer x:Name='PART_ContentHost' Margin='{TemplateBinding Padding}' VerticalAlignment='Center'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='b' Property='BorderBrush' Value='{DynamicResource Accent}'/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType='CheckBox'>
    <Setter Property='Foreground' Value='{DynamicResource Text}'/>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='FocusVisualStyle' Value='{x:Null}'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='CheckBox'>
          <StackPanel Orientation='Horizontal' Background='Transparent'>
            <Border x:Name='box' Width='18' Height='18' CornerRadius='5' BorderThickness='1.5' VerticalAlignment='Center'
                    BorderBrush='{DynamicResource Dashed}' Background='{DynamicResource Input}'>
              <Path x:Name='mark' Data='M3.5,8.5 L6.8,11.8 L12.5,5' Stroke='{DynamicResource AccentText}' StrokeThickness='2'
                    StrokeStartLineCap='Round' StrokeEndLineCap='Round' Visibility='Collapsed'/>
            </Border>
            <ContentPresenter Margin='8,0,0,0' VerticalAlignment='Center'/>
          </StackPanel>
          <ControlTemplate.Triggers>
            <Trigger Property='IsChecked' Value='True'>
              <Setter TargetName='box' Property='Background' Value='{DynamicResource Accent}'/>
              <Setter TargetName='box' Property='BorderBrush' Value='{DynamicResource Accent}'/>
              <Setter TargetName='mark' Property='Visibility' Value='Visible'/>
            </Trigger>
            <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='box' Property='BorderBrush' Value='{DynamicResource Accent}'/></Trigger>
            <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.45'/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType='ToolTip'>
    <Setter Property='Background' Value='{DynamicResource Panel}'/>
    <Setter Property='Foreground' Value='{DynamicResource Text}'/>
    <Setter Property='BorderBrush' Value='{DynamicResource Border}'/>
  </Style>

  <!-- Thin scroll bars that follow the theme -->
  <ControlTemplate x:Key='VScroll' TargetType='ScrollBar'>
    <Track x:Name='PART_Track' IsDirectionReversed='True'>
      <Track.Thumb>
        <Thumb>
          <Thumb.Template>
            <ControlTemplate TargetType='Thumb'><Border CornerRadius='4' Margin='2' Background='{DynamicResource Scroll}'/></ControlTemplate>
          </Thumb.Template>
        </Thumb>
      </Track.Thumb>
    </Track>
  </ControlTemplate>
  <ControlTemplate x:Key='HScroll' TargetType='ScrollBar'>
    <Track x:Name='PART_Track' IsDirectionReversed='False'>
      <Track.Thumb>
        <Thumb>
          <Thumb.Template>
            <ControlTemplate TargetType='Thumb'><Border CornerRadius='4' Margin='2' Background='{DynamicResource Scroll}'/></ControlTemplate>
          </Thumb.Template>
        </Thumb>
      </Track.Thumb>
    </Track>
  </ControlTemplate>
  <Style TargetType='ScrollBar'>
    <Setter Property='Background' Value='Transparent'/>
    <Setter Property='Width' Value='10'/>
    <Setter Property='Template' Value='{StaticResource VScroll}'/>
    <Style.Triggers>
      <Trigger Property='Orientation' Value='Horizontal'>
        <Setter Property='Width' Value='Auto'/>
        <Setter Property='Height' Value='10'/>
        <Setter Property='Template' Value='{StaticResource HScroll}'/>
      </Trigger>
    </Style.Triggers>
  </Style>
</ResourceDictionary>";
    }
}
