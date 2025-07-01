
//Код взять из книги "Высокопроизводительный код .NET Uotson B. Глава 2 - "Управление памятью"



/*
using Microsoft.Diagnostics.Runtime;
using System;

class BitfinexLOHChecker
{
    private static void PrintLOHObjects(ClrRuntime clr)
    {
        Console.WriteLine("LOH Objects (limit:10)");
        int objectCount = 0;
        const int MaxObjectCount = 10;

        if (!clr.Heap.CanWalkHeap)
        {
            Console.WriteLine("Cannot walk the heap.");
            return;
        }

        foreach (var segment in clr.Heap.Segments)
        {
            // Check if the segment is the Large Object Heap (LOH)
            if (segment.Kind == GCSegmentKind.Large)
            {
                Console.WriteLine($"LOH Segment: Start={segment.Start:X}, End={segment.End:X}");

                // Enumerate objects in the LOH
                var memoryRange = segment.Generation0;
                for (var i = memoryRange.Start; i < memoryRange.End; i++ )
                foreach (ulong objAddr in segment.Generation0)
                {
                    var type = clr.Heap.GetObjectType(objAddr);
                    if (type == null)
                        continue;

                    var obj = new ClrObject(objAddr, type);
                    Console.WriteLine($"  {obj.Address:X16} {obj.Type.Name} (Size: {obj.Size:n0} bytes)");

                    if (++objectCount >= MaxObjectCount)
                        return; // Stop after 10 objects
                }
            }
        }
    }
}
*/