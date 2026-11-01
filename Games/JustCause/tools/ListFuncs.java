import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.*;
import ghidra.program.model.listing.*;

public class ListFuncs extends GhidraScript {
    public void run() throws Exception {
        long lo = Long.parseLong(getScriptArgs().length > 0 ? getScriptArgs()[0] : "10003300", 16);
        FunctionManager fm = currentProgram.getFunctionManager();
        for (Function f : fm.getFunctions(true)) {
            long e = f.getEntryPoint().getOffset();
            if (e < lo) continue;
            AddressSetView body = f.getBody();
            println("FUNC " + f.getEntryPoint() + "-" + Long.toHexString(body.getMaxAddress().getOffset())
                + " size=" + body.getNumAddresses() + " " + f.getName());
        }
    }
}
