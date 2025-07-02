
//Код взять из книги "Высокопроизводительный код .NET Uotson B. Глава 2 - "Управление памятью"

using Microsoft.Diagnostics.Runtime;

class BitfinexLOHChecker
{
    public static void GetLOHObjectsCount()
    {
        using (DataTarget target = DataTarget.AttachToProcess(System.Diagnostics.Process.GetCurrentProcess().Id, false))
        {
            ClrRuntime runtime = target.ClrVersions[0].CreateRuntime();
            ClrHeap heap = runtime.Heap;

            if (!heap.CanWalkHeap)
            {
                return;
            }

            var gcCounts = new Dictionary<string, long>();

            foreach (ClrSegment seg in heap.Segments)
            {
                string segmentName = seg.Kind.ToString();
                long objectCount = seg.EnumerateObjects().Count();

                if (seg.Kind == GCSegmentKind.Large)
                {
                    gcCounts[segmentName] = objectCount;
                }
                else if (seg.Kind == GCSegmentKind.Generation0 ||
                         seg.Kind == GCSegmentKind.Generation1 ||
                         seg.Kind == GCSegmentKind.Generation2)
                {
                    gcCounts[segmentName] = objectCount;
                }
            }
            foreach (var entry in gcCounts)
            {
                Console.WriteLine($"{entry.Key}: {entry.Value}");
            }
        }
    }
}
//Оригинальный код
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