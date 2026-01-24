import subprocess
import os
import magic  # pip install python-magic

def identify_file_type(file_path):
    mime = magic.from_file(file_path)
    if 'DEX' in mime or file_path.endswith('.apk'):
        return 'DEX'
    elif 'PE32' in mime:
        return 'PE'
    elif 'ELF' in mime:
        return 'ELF'
    else:
        return 'UNKNOWN'

def run_jadx(apk_path, output_dir):
    subprocess.run(['jadx', '-d', output_dir, apk_path], check=True)

def run_ghidra(file_path, output_dir, ghidra_path, project_dir):
    subprocess.run([
        ghidra_path, 'analyzeHeadless', project_dir, 'OrchestratorProject',
        '-import', file_path, '-scriptPath', '.', '-postScript', 'DecompilerScript.java', '-deleteProject',
        '-overwrite', '-scriptlog', os.path.join(output_dir, 'ghidra.log')
    ], check=True)

def dispatcher(file_path, output_dir, ghidra_path, ghidra_project_dir):
    ftype = identify_file_type(file_path)
    if ftype == 'DEX':
        run_jadx(file_path, output_dir)
        engine = 'jadx'
    elif ftype in ('PE', 'ELF'):
        run_ghidra(file_path, output_dir, ghidra_path, ghidra_project_dir)
        engine = 'ghidra'
    else:
        raise Exception('Unsupported file type')
    return engine
