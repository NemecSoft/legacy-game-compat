import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.*;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.*;
import ghidra.program.model.symbol.Reference;

public class War3D3D9 extends GhidraScript {
    public void run() throws Exception {
        DecompInterface di = new DecompInterface();
        di.openProgram(currentProgram);
        FunctionManager fm = currentProgram.getFunctionManager();
        Listing lst = currentProgram.getListing();
        java.util.HashSet<String> done = new java.util.HashSet<>();
        DataIterator it = lst.getDefinedData(true);
        while (it.hasNext() && !monitor.isCancelled()) {
            Data d = it.next();
            if (!d.hasStringValue()) continue;
            String s = String.valueOf(d.getValue());
            if (!(s.contains("d3d9") || s.contains("Direct3DCreate9") || s.contains("Could not initialize graphics"))) continue;
            String show = s.length() > 100 ? s.substring(0, 100) + "..." : s;
            println("\n=== STRING " + d.getAddress() + " : " + show);
            for (Reference r : getReferencesTo(d.getAddress())) {
                Address from = r.getFromAddress();
                Function f = fm.getFunctionContaining(from);
                println("    REF " + from + " in " + (f == null ? "<no func>" : f.getName() + "@" + f.getEntryPoint()));
                if (f == null) continue;
                String key = f.getEntryPoint().toString();
                if (!done.add(key)) continue;
                DecompileResults res = di.decompileFunction(f, 180, monitor);
                if (res != null && res.decompileCompleted()) {
                    String c = res.getDecompiledFunction().getC();
                    println("-------- C " + f.getName() + " @ " + f.getEntryPoint() + " --------");
                    println(c.length() > 16000 ? c.substring(0, 16000) : c);
                } else {
                    println("    (decompile failed)");
                }
            }
        }
        di.dispose();
    }
}