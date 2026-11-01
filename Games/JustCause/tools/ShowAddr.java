import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.*;
import ghidra.program.model.mem.*;
import ghidra.program.model.listing.*;

public class ShowAddr extends GhidraScript {
    public void run() throws Exception {
        println("func count " + currentProgram.getFunctionManager().getFunctionCount());
        Address a = currentProgram.getAddressFactory().getDefaultAddressSpace().getAddress(0x1000347fL);
        println("instr at " + a + ": " + getInstructionAt(a));
        println("func at 0x1000347f: " + getFunctionContaining(a));
        Address b = currentProgram.getAddressFactory().getDefaultAddressSpace().getAddress(0x10003c17L);
        println("instr at " + b + ": " + getInstructionAt(b));
        println("func at 0x10003c17: " + getFunctionContaining(b));
        for (MemoryBlock mb : currentProgram.getMemory().getBlocks())
            println("BLOCK " + mb.getName() + " " + mb.getStart() + "-" + mb.getEnd() + " exec=" + mb.isExecute());
        int n = 0;
        for (Function f : currentProgram.getFunctionManager().getFunctions(true)) {
            if (n++ < 25) println("FUNC " + f.getEntryPoint() + " " + f.getName());
        }
    }
}
