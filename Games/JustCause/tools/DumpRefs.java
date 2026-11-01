import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.*;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.*;
import ghidra.program.model.symbol.Reference;

public class DumpRefs extends GhidraScript {
    static final String[] NEEDLES = {
        "overlay loaded", "UTF8 initialization started", "three-byte UTF-8 decoder patched",
        "decoder hook create", "inline hooks reset", "final counts",
        "Direct3DCreate9", "CreateDevice hook failed", "Chinese font ready",
        "native subtitle pixel readback failed", "D3DXCreateFontW", "subtitle"
    };

    public void run() throws Exception {
        Listing lst = currentProgram.getListing();
        DecompInterface di = new DecompInterface();
        di.openProgram(currentProgram);
        FunctionManager fm = currentProgram.getFunctionManager();
        DataIterator it = lst.getDefinedData(true);
        java.util.HashSet<String> done = new java.util.HashSet<>();
        while (it.hasNext() && !monitor.isCancelled()) {
            Data d = it.next();
            if (!d.hasStringValue()) continue;
            String s = String.valueOf(d.getValue());
            boolean hit = false;
            for (String n : NEEDLES) if (s.contains(n)) { hit = true; break; }
            if (!hit) continue;
            println("\n=== STRING " + d.getAddress() + " : " + s);
            for (Reference r : getReferencesTo(d.getAddress())) {
                Address from = r.getFromAddress();
                Function f = fm.getFunctionContaining(from);
                println("    REF " + from + " in " + (f == null ? "<no func>" : f.getName() + "@" + f.getEntryPoint()));
                if (f == null) continue;
                String key = f.getEntryPoint().toString();
                if (!done.add(key)) continue;
                DecompileResults res = di.decompileFunction(f, 120, monitor);
                if (res != null && res.decompileCompleted()) {
                    println("-------- C " + f.getName() + " @ " + f.getEntryPoint() + " --------");
                    println(res.getDecompiledFunction().getC());
                } else {
                    println("---- decompile failed " + f.getName());
                }
            }
        }
    }
}
