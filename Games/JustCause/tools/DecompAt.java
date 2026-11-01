import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.*;
import ghidra.app.cmd.disassemble.DisassembleCommand;
import ghidra.program.model.address.*;
import ghidra.program.model.listing.*;

public class DecompAt extends GhidraScript {
    public void run() throws Exception {
        DecompInterface di = new DecompInterface();
        di.openProgram(currentProgram);
        for (String arg : getScriptArgs()) {
            Address a = toAddr(Long.parseLong(arg, 16));
            Function f = getFunctionContaining(a);
            if (f == null) {
                DisassembleCommand cmd = new DisassembleCommand(a, null, true);
                cmd.applyTo(currentProgram, monitor);
                f = createFunction(a, "sub_" + Long.toHexString(a.getOffset()));
                println("created function at " + a + " -> " + f);
            }
            if (f == null) { println("NO FUNCTION at " + a); continue; }
            DecompileResults res = di.decompileFunction(f, 180, monitor);
            println("======== C " + f.getName() + " @ " + f.getEntryPoint() + " ========");
            if (res != null && res.decompileCompleted())
                println(res.getDecompiledFunction().getC());
            else
                println("decompile failed");
        }
    }
}
