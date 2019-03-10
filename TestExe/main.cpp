#include <iostream>
#include <algorithm>
#include <fstream>
#include <string>
#include <vector>

////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////
class FileBuffer {
    
public:
    char* _pBytes = nullptr;
    int32_t _iLength = 0;
    void alloc(int32_t iLength) {
        _iLength = iLength;
        _pBytes = new char[iLength];
    }
    std::string toString() { 
        std::string ret = "";
        if(_pBytes == nullptr || _iLength <=0){
            return ret;
        }
        char* b2 = new char[_iLength + 1];
        memset(b2, 0, _iLength + 1);
        memcpy(b2, _pBytes, _iLength);
        ret.assign(b2, 0, _iLength+1);
        delete[] b2;
        return ret;
    }
    void loadFileDisk(std::string loc) {
        _iLength = 0;
        if (_pBytes != nullptr) {
            delete[] _pBytes;
        }

        std::fstream fs;
        fs.open(loc, std::ios::in | std::ios::binary);
        if (!fs.good()) {
            fs.close();
            throw new std::exception("Failed to open file.");
        }
        fs.seekg(0, std::ios::end);
        int flen = fs.tellg();
        fs.seekg(0, std::ios::beg);

        alloc(flen);

        fs.read(_pBytes, _iLength);
        fs.close();

    }
};
////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////
class FileEntry {
public:
    std::string _strUnformattedPath;
    std::string _strLoc;
    int32_t _iOff;
    int32_t _iSize;
};
////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////
class FileTable {
    std::vector<FileEntry*> _vecEntries;
    int32_t _iExeLenBytes = 0;
    std::string _strExeLoc;
    int32_t _iTableLenBytes = 0;

    std::string lower(std::string in) {
        std::transform(in.begin(), in.end(), in.begin(), ::tolower);
        return in;
    }
    int32_t parseInt32(FileBuffer& fb, int32_t& off) {
        int32_t ret;
        ret = *((int32_t*)(fb._pBytes + off));
        off += sizeof(int32_t);
        return ret;
    }
    std::string parseStr(FileBuffer& fb, int32_t& off) {
        int32_t iCount = parseInt32(fb, off);

        char* tmp = new char[iCount + 1];
        memset(tmp, 0, iCount + 1);
        memcpy(tmp, fb._pBytes + off, iCount);
        off += iCount;

        std::string ret;
        ret.assign(tmp);
        delete[] tmp;

        return ret;
    }
    std::string formatPath(std::string path) {
        //Use the stringutil here!
        std::string ret = "";
        for (char c : path) {
            if (c == '\\') {
                ret += "/";
            }
            else { 
                ret += c;
            }
        }

        return ret;
    }
    FileEntry* getEntry(std::string fileLoc) {
        std::string locLow = lower(formatPath(fileLoc));

        for (FileEntry* fe : _vecEntries) {
            std::string feLow = lower(fe->_strLoc);
            if (feLow.compare(locLow) == 0) {
                return fe;
            }
        }
        return nullptr;
    }
public:
    FileTable(){
    }
    virtual ~FileTable() { 
        for(FileEntry* fe : _vecEntries){
            delete fe;
        }
        _vecEntries.resize(0);
    }

    bool getFile(std::string fileLoc, FileBuffer& fb) {
        FileEntry* fe = getEntry(fileLoc);
        if(fe == nullptr){
            throw std::exception((std::string("Failed to get file entry for ") + fileLoc).c_str());
        }
        std::fstream fs;
        fs.open(_strExeLoc.c_str(), std::ios::in | std::ios::binary);
        if(!fs.good()){
            fs.close();
            return false;
        }
        fb.alloc(fe->_iSize);
        size_t iFileOff = _iExeLenBytes + _iTableLenBytes + fe->_iOff;
        fs.seekg(0, std::ios::end);
        size_t iExePackSize = fs.tellg();
        if( iFileOff + fe->_iSize > iExePackSize){
            throw std::exception("ERROR File overrun: size of file is greater than the packed exe size.");
        }
        fs.seekg(iFileOff, std::ios::beg);
        fs.read(fb._pBytes, fe->_iSize);
        fs.close();
        return true;
    }
    FileBuffer loadExe(){
        FileBuffer fb;
        fb.loadFileDisk(_strExeLoc);
        return fb;
    }
    void build(std::string exeLoc) {
        _strExeLoc = exeLoc;
        FileBuffer fb = loadExe();

        int32_t tmp;
        //the last 4 bytes are the EXE length.
        char sig0 = *(fb._pBytes + (fb._iLength - 4));
        char sig1 = *(fb._pBytes + (fb._iLength - 3));
        char sig2 = *(fb._pBytes + (fb._iLength - 2));
        char sig3 = *(fb._pBytes + (fb._iLength - 1));

        if(sig0 != 'a' || sig1 != 's' || sig2 != 'd' || sig3 != 'f'){
            throw std::exception("Error: Exe is not packed. Signature not present");
        }

        _iExeLenBytes = parseInt32(fb, (tmp = fb._iLength - 8));
        std::cout << "ExeLen: " << _iExeLenBytes << std::endl;

        //Start parsing at the end fo the exe
        int32_t iByteIdx = _iExeLenBytes;

        // 8 bytes, table length (total) and num entries
        _iTableLenBytes = parseInt32(fb, iByteIdx);
        int32_t iNumEntries = parseInt32(fb, iByteIdx);
        std::cout << "Num Entries: " << iNumEntries << std::endl;

        for (int32_t iEntry = 0; iEntry < iNumEntries; ++iEntry) {
            FileEntry* fe = new FileEntry();
            fe->_strUnformattedPath = parseStr(fb, iByteIdx);
            fe->_strLoc = formatPath(fe->_strUnformattedPath);
            fe->_iOff = parseInt32(fb, iByteIdx);
            fe->_iSize = parseInt32(fb, iByteIdx);
            _vecEntries.push_back(fe);
        }
    }
    void print() {
        std::cout << "Files:" << std::endl;
        for (FileEntry* fe : _vecEntries) {
            std::cout << "  Loc:" << fe->_strLoc << std::endl;
            std::cout << "  Off:" << fe->_iOff << std::endl;
            std::cout << " Size: " << fe->_iSize << std::endl;
            FileBuffer tmp;
            if(getFile(fe->_strLoc, tmp)){
                std::cout << " Data: " << tmp.toString() << std::endl;
            }
            else {
                std::cout<< " ERROR getting file data." << std::endl;
            }
        
        }
    }
};
int fileExists(char *filename) {
    struct stat   buffer;
    return (stat(filename, &buffer) == 0);
}
void printAndExit(std::string str, bool error) {
    std::cout << str << std::endl;
    std::cin.get();
    exit(0);
}
int main(int argc, char**argv) {
    char* exeName = argv[0];
    try {
        std::cout << "Press any key to load from the baked binary" << std::endl;
        char c = std::cin.get();
        
        if (fileExists(exeName)) {
            FileTable ft;
            ft.build(exeName);
            ft.print();
        }
        else {
            printAndExit(std::string("Exe '") + exeName + std::string("' was not found."), true);
        }
        
    }
    catch (std::exception ex) {
        std::cout << ex.what() << std::endl;
    }
    std::cin.get();
    std::cin.get();
    return 0;
}