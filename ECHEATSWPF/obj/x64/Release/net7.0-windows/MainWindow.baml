   M S B A M L   `   `   `  ���� C  ?AimmyWPF, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null#clr-namespace:AimmyWPFAimmyWPF      AimmyWPF.MainWindow   W SPresentationCore, Version=7.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=7.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35e aSystem.Windows.Controls.Ribbon, Version=7.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089R NWindowsBase, Version=7.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35F 9http://schemas.microsoft.com/winfx/2006/xaml/presentation     R NSystem.Xaml, Version=7.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     @d2http://schemas.microsoft.com/expression/blend/2008     Jmc;http://schemas.openxmlformats.org/markup-compatibility/2006     _ [MaterialDesignThemes.Wpf, Version=4.9.0.0, Culture=neutral, PublicKeyToken=df2a72020bd7962aFmaterialDesign1http://materialdesigninxaml.net/winfx/xaml/themes  "localclr-namespace:AimmyWPF  -     � Title$  Aimmy��$	��378��$	��564�� � WindowStyle$
 None=� � Topmost$
 True�� � 
ResizeMode$ NoResize=� � WindowStartupLocation$ CenterScreen=� � AllowsTransparency$
 True��
������ .��� .+��� -   ���	#FF0F0F0F �� CornerRadius$ 5,5,5,5�����	#FF1E1E1E$��2q�._�� .+�� $	��Top=�$��40��.+��� $
��Left=�$��92��
�������$��ECHEATS��$6��0/AimmyWPF;component/Fonts/#Atkinson Hyperlegible)�$��Center=�$��15,10,15,10q�$��13(��� -    ��Name$
 Exit��$
��10,0q�$��0,0,0,0q�$	����$��Segoe MDL2 Assets)�$��Right=�$��10,0,0,0q�
�������$
��Auto��$��10(���Z� ��Z� �� -   $ Minimize��$
��10,0q�$��0,0,0,0q�$	����$��Segoe MDL2 Assets)�$��Right=�
�������$
��Auto��$��10(�$��0,0,30,0q���Z� ��Z� � -    �Name$ MenuBar��$��0,40,0,0q�.+��� $	��Top=�$
��10,0q�$��34��$ 5,5,5,5�����	#FF1E1E1E$��1q�._�� .+��� -   	 ��Name$	 MenuHighlighter�����#3e8fb0$ 1��$��10, 30, 414, 0q��� -   $ 
Selection1��$��0,0,0,0q�$��0,0,0,0q�$��Aim Aligner��$6��0/AimmyWPF;component/Fonts/#Atkinson Hyperlegible)�$
��Left=�$	��134��+  %MaterialDesignThemes.Wpf.RippleAssist
   
IsDisabled$

 True�����	#FF3E8FB0
 � Tag$ AimMenu����Z� ��Z� �� -   $ 
Selection2��$��0,0,0,0q�$��0,0,0,0q�$��Auto Trigger��$6��0/AimmyWPF;component/Fonts/#Atkinson Hyperlegible)�$
��Left=�$	��136��$��	134,0,0,0q�$

 True��
�������$ TriggerMenu����Z� ��Z� �� -	   $ 
Selection3��$��0,0,0,0q�$��0,0,0,0q�$��Model Selector��$6��0/AimmyWPF;component/Fonts/#Atkinson Hyperlegible)�$
��Left=�$	��134��$��	270,0,0,0q�$

 True��
�������$ SelectorMenu����Z� ��Z� �� -
   $ 
Selection4��$��0,0,0,0q�$��0,0,0,0q�$��Settings��$6��0/AimmyWPF;component/Fonts/#Atkinson Hyperlegible)�$
��Left=�$	��134��$��	404,0,0,0q�$

 True��
�������$ SettingsMenu����Z� ��Z� � -   $ Menus��$��10,84,10,10q�.+��� -    ��Name$ AimMenu��$��Hidden=� �� PanningMode$ VerticalOnly=�$y�Visible=�.���� -    ��Name$ AimScroller���� -   $ TriggerMenu��$��Hidden=�$ VerticalOnly=�$y�	Collapsed=�.���� -   $ TriggerScroller��� -   $ SelectorMenu��$y�	Collapsed=�.+��� $
��Left=�.@��� $��0q�$��0q�$��33������    MaterialDesignPaper#���   $��Local Models��$��11(�.��� .+��� ���	#FF191919���	#FF1E1E1E$��2q�$ 5��$��0,0,0,44q�._��� -    ��Name$ SelectorListBox�����	#DDFFFFFF$6��0/AimmyWPF;component/Fonts/#Atkinson Hyperlegible)�$��0q�$��2q��� ���	#FF191919���	#FF1E1E1E$��2q�$ 5��$��0,0,0,0q�$��Bottom=�$��39��._��� -    ��Name$ SelectedModelNotifier�����	#DDFFFFFF$��Loaded Model: N/A��$��Atkinson Hyperlegible)�$��10q��� $��0q�$��0q�$��33������ #���   $��Local Configs��$��11(�.��� .+��� ���	#FF191919���	#FF1E1E1E$��2q�$ 5��$��0,0,0,44q�._��� -   $ ConfigSelectorListBox�����	#DDFFFFFF$6��0/AimmyWPF;component/Fonts/#Atkinson Hyperlegible)�$��0q�$��2q��� ���	#FF191919���	#FF1E1E1E$��2q�$ 5��$��0,0,0,0q�$��Bottom=�$��39��._��� -   $ SelectedConfigNotifier�����	#DDFFFFFF$��Loaded Model: N/A��$��Atkinson Hyperlegible)�$��10q��� $��0q�$��0q�$��33������ #���   $��Downloadable Models��$��11(�.��� .+��� -   $ LackOfModelsText��$y�	Collapsed=�$��Center=�$��Center=�$N��HThere is no downloadable models at the moment, please check again later.�����	#DDFFFFFF�� -   $ ModelStoreMenu��$��Hidden=�$ VerticalOnly=�.���� -   $ ModelStoreScroller���� -   $ SettingsMenu��$��Hidden=�$ VerticalOnly=�$y�	Collapsed=�.���� -   $ SettingsScroller���� -   $	 MoreInfoBox��$��Bottom=�$	��180��$��
5,0,5,-180q����	#FF191919���	#FF1E1E1E$��2q�$ 5��$y�	Collapsed=�._�� .+��� $
��Left=�$��93��
�������$��	More Info��$6��0/AimmyWPF;component/Fonts/#Atkinson Hyperlegible)�$��Center=�$��15,10,15,10q�$��13(�$	��Top=�$��40���� -   $ MoreInfoExit��$
��10,0q�$��0,0,0,0q�$	����$��Segoe MDL2 Assets)�$��Right=�$��0,4,0,0q�
�������$��32��$��10(�$	��Top=�$��30����Z� ��Z� �� $��15,40,15,15q�.���� -    ��Name$ InfoText��
�������$��Atkinson Hyperlegible)�$��Hello��$��14(�$
��Wrap=�