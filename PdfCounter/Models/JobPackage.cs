using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using DynamicData.Binding;
namespace PdfCounter.Models;
public class JobPackage
{
    public string Name { get; set; } = "";
    public string? PdfFileName { get; set; }          
    public string? PdfPath { get; set; }
    public int SamplePageIndex { get; set; } = 0;
    // public ObservableCollection<OverlayBox>? OverlayBoxes { get; set;}
    // public ObservableCollection<ChunkRow>? ChunkRows { get; set; }        
    public List<ExtractField>? Fields { get; set; } = new();  
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
