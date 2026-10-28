using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

public sealed class FeedbackWindow : Window {
    readonly ComboBox category;readonly TextBox title,description,contact;readonly CheckBox environment;
    readonly TextBlock status;readonly Button submit,copy;readonly StackPanel form;readonly CancellationTokenSource lifetime=new CancellationTokenSource();
    CancellationTokenSource operation;bool closed;string receipt;
    public FeedbackWindow(Window owner) {
        Owner=owner;Title="反馈问题";Width=600;Height=690;MinWidth=480;MinHeight=540;MaxHeight=SystemParameters.WorkArea.Height-32;
        Background=Theme.Bg;Foreground=Theme.Ink;Resources=Theme.Resources();FontFamily=Theme.Chinese;FontSize=15;UseLayoutRounding=true;SnapsToDevicePixels=true;
        TextOptions.SetTextFormattingMode(this,TextFormattingMode.Display);WindowStartupLocation=owner==null?WindowStartupLocation.CenterScreen:WindowStartupLocation.CenterOwner;
        var root=new Grid{Margin=new Thickness(28)};root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});Content=root;
        var heading=new StackPanel{Margin=new Thickness(0,0,0,20)};heading.Children.Add(Theme.Text("让 AQ Server 更好用",22,Theme.Ink,false,true));var hint=Theme.Text("描述遇到的问题或建议，提交后可获得反馈编号。",14,Theme.Muted);hint.Margin=new Thickness(0,8,0,0);hint.TextWrapping=TextWrapping.Wrap;heading.Children.Add(hint);root.Children.Add(heading);
        form=new StackPanel();var scroll=new ScrollViewer{Content=form,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Grid.SetRow(scroll,1);root.Children.Add(scroll);
        form.Children.Add(Theme.Text("反馈类型",14,Theme.Muted));category=new ComboBox{ItemsSource=new[]{"功能问题","界面与交互","功能建议","其他"},SelectedIndex=0,MinHeight=40,Margin=new Thickness(0,8,0,18)};form.Children.Add(category);
        title=Field("问题标题",false,100);description=Field("问题详情 / 复现步骤",true,8000);contact=Field("联系方式（选填）",false,200);
        environment=new CheckBox{Content="附上系统版本与显示缩放（可选）",IsChecked=false,Margin=new Thickness(0,0,0,16)};form.Children.Add(environment);
        var privacy=Theme.Text("仅发送此表单、软件版本和平台。请勿填写密码、密钥等敏感信息。",13,Theme.Muted);privacy.TextWrapping=TextWrapping.Wrap;privacy.LineHeight=21;
        var footer=new StackPanel{Margin=new Thickness(0,20,0,0)};status=Theme.Text("",14,Theme.Muted);status.TextWrapping=TextWrapping.Wrap;status.Margin=new Thickness(0,0,0,12);footer.Children.Add(privacy);privacy.Margin=new Thickness(0,0,0,12);footer.Children.Add(status);
        var actions=new Grid();actions.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});actions.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});copy=Theme.Nav("复制反馈编号",()=>{if(receipt!=null)Clipboard.SetText(receipt);});copy.Visibility=Visibility.Collapsed;copy.HorizontalAlignment=HorizontalAlignment.Left;actions.Children.Add(copy);
        submit=Theme.Button("提交反馈",Send);submit.MinWidth=114;Grid.SetColumn(submit,1);actions.Children.Add(submit);footer.Children.Add(actions);Grid.SetRow(footer,2);root.Children.Add(footer);
        Closed+=(s,e)=>{closed=true;lifetime.Cancel();};
    }
    TextBox Field(string label,bool multiline,int limit){form.Children.Add(Theme.Text(label,14,Theme.Muted));var field=new TextBox{FontSize=15,MaxLength=limit,Margin=new Thickness(0,8,0,18),MinHeight=40,VerticalContentAlignment=multiline?VerticalAlignment.Top:VerticalAlignment.Center,AcceptsReturn=multiline,TextWrapping=multiline?TextWrapping.Wrap:TextWrapping.NoWrap};if(multiline){field.Height=150;field.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;}form.Children.Add(field);return field;}
    async void Send() {
        if(operation!=null){operation.Cancel();return;}
        var payload=new FeedbackPayload{category=new[]{"bug","ui","suggestion","other"}[category.SelectedIndex],title=title.Text.Trim(),description=description.Text.Trim(),contact=contact.Text.Trim(),environment=environment.IsChecked==true?Environment.OSVersion.VersionString+" · "+(Environment.Is64BitOperatingSystem?"64-bit":"32-bit")+" · scale "+(PresentationSource.FromVisual(this)==null?1:PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice.M11).ToString("0.##"):""};
        try{FeedbackClient.Validate(payload);}catch(Exception e){status.Text=e.Message;status.Foreground=Theme.Red;return;}
        operation=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);var active=operation;submit.Content="取消提交";form.IsEnabled=false;status.Text="正在提交…";status.Foreground=Theme.Muted;
        try{receipt=await FeedbackClient.Submit(payload,active.Token);if(!closed){status.Text="已收到反馈 · "+receipt;status.Foreground=Theme.Green;copy.Visibility=Visibility.Visible;submit.Visibility=Visibility.Collapsed;}}
        catch(Exception e){if(!closed){status.Text=FeedbackClient.FriendlyError(e);status.Foreground=Theme.Red;}}
        finally{active.Dispose();operation=null;if(!closed){submit.Content="提交反馈";form.IsEnabled=receipt==null;}}
    }
}
