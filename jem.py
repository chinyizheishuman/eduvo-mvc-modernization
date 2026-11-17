import os

def list_files(startpath):
    print(f"Directory listing for: {os.path.abspath(startpath)}")
    print("-" * 30)

    # Walk through the directory structure
    for root, dirs, files in os.walk(startpath):
        # Calculate depth (how many folders deep we are)
        level = root.replace(startpath, '').count(os.sep)
        
        # Determine indentation
        indent = '  ' * level
        
        # Print the current folder name
        # os.path.basename(root) gives just the folder name, not the full path
        folder_name = os.path.basename(root)
        
        # Don't print the root dot '.' if you prefer clean output, 
        # but usually, we print the folder name unless it's the start.
        if level == 0:
            print(folder_name if folder_name else ".")
        else:
            print(f"{indent}- {folder_name}")
        
        # Print all files in this specific folder
        sub_indent = '  ' * (level + 1)
        for f in files:
            print(f"{sub_indent}- {f}")

if __name__ == "__main__":
    # '.' means the current directory
    list_files('.')