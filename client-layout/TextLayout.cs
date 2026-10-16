using System;
using System.Collections.Generic;
using UnityEngine;
namespace ProjectBrainiac.WeekendTextLayout
{
 public static class TextLayout
 {
  struct Key : IEquatable<Key>
  {
   public GUIStyle Style; public string Text; public int W,H,Size;
   public bool Equals(Key k) => ReferenceEquals(Style,k.Style) && Text==k.Text && W==k.W && H==k.H && Size==k.Size;
   public override bool Equals(object o) => o is Key && Equals((Key)o);
   public override int GetHashCode() { unchecked { return (((Style.GetHashCode()*397 ^ Text.GetHashCode())*397 ^ W)*397 ^ H)*397 ^ Size; } }
  }
  static readonly Dictionary<Key,GUIStyle> Cache=new Dictionary<Key,GUIStyle>();
  public static void Label(Rect rect,string text,GUIStyle style)
  {
   if(style==null || string.IsNullOrEmpty(text) || rect.width<=0 || rect.height<=0) { GUI.Label(rect,text,style); return; }
   var key=new Key {Style=style,Text=text,W=(int)(rect.width*10),H=(int)(rect.height*10),Size=style.fontSize};
   GUIStyle fitted;
   if(!Cache.TryGetValue(key,out fitted))
   {
    var content=new GUIContent(text);
    float height=style.CalcHeight(content,rect.width);
    bool horizontal=!style.wordWrap && style.CalcSize(content).x>rect.width;
    fitted=style;
    if(height>rect.height+0.1f || horizontal)
    {
     fitted=new GUIStyle(style) { clipping=TextClipping.Clip };
     int original=style.fontSize>0 ? style.fontSize : 13;
     if(horizontal && rect.height>=2*(original+2))fitted.wordWrap=true;
     for(int size=original;size>=6;size--)
     {
      fitted.fontSize=size;
      if(fitted.CalcHeight(content,rect.width)<=rect.height+0.1f && (fitted.wordWrap || fitted.CalcSize(content).x<=rect.width))break;
     }
    }
    if(Cache.Count>=2048)Cache.Clear();
    Cache[key]=fitted;
   }
   GUI.Label(rect,text,fitted);
  }
 }
}
