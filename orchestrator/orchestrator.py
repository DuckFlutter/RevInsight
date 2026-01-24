import os
from dispatcher import dispatcher
from logic_validator import generate_audit_report
# from memory_dumper import dump_memory  # Uncomment if needed

def main(file_path, output_dir, ghidra_path, ghidra_project_dir, packer=None):
    engine = dispatcher(file_path, output_dir, ghidra_path, ghidra_project_dir)
    if packer and packer.lower() in ['themida', 'obfuscated']:
        pass  # dump_memory(file_path, os.path.join(output_dir, 'memdump.txt'))
    generate_audit_report(file_path, packer, engine, output_dir, os.path.join(output_dir, 'audit_report.json'))

if __name__ == "__main__":
    # Example usage, replace with argparse for CLI
    main('input.bin', 'output', '/path/to/ghidra/support/analyzeHeadless', '/tmp/ghidra_project', packer='Themida')
