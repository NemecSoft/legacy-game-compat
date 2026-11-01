import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.*;
import ghidra.program.model.listing.*;

public class DumpAsm extends GhidraScript {
    public void run() throws Exception {
        String[] a = getScriptArgs();
        Address start = toAddr(Long.parseLong(a[0], 16));
        Address end = toAddr(Long.parseLong(a[1], 16));
        InstructionIterator it = currentProgram.getListing().getInstructions(start, true);
        while (it.hasNext()) {
            Instruction ins = it.next();
            if (ins.getAddress().compareTo(end) > 0) break;
            println(ins.getAddress() + "  " + ins.toString() + "   [" + bytes(ins) + "]");
        }
    }
    String bytes(Instruction ins) {
        StringBuilder sb = new StringBuilder();
        try {
            byte[] b = ins.getBytes();
            for (byte x : b) sb.append(String.format("%02x ", x));
        } catch (Exception e) {}
        return sb.toString().trim();
    }
}
