# DTOGenerator
Source generator that automatically generates DTOs based on existing models
Has 4 attributes:
- ### HasDTOAttribute
  Use this in classes or structs (not tested in structs) to tell the generator which models should have a DTO
  
  **DTOClassName** can be set to modify the DTO's name, "\[class\]" will be substituted by the model's name, the default is \[class\]DTO
  
  **ConversionForm** Enum that specifies conversions flags for Model -> DTO and DTO -> Model. Multiple flags can be set, but Explicit overrides Implicit, and None is Ignored if other flag is set 
  - None
  - Explicit
  - Implicit
  - StaticMethods
  - ReferenceMethods 
  
  the default is ConversionForm.Explicit

- ### DTOIgnoreAttribute
  Use this in classes/structs properties/fields to ignore them in the final DTO, required members will be set to null

- ### HasConversionAttribute
  Specifies that this varible will swich type in this class DTO

  **hasConversionForm**  Enum that specifies how the member should be converted, this conversion method should be available in the DTO type of member
  - Explicit
  - Implicit
  - StaticMethods
  
  **convertedType** Which type the member will be converted to

- ### HasIndirectConversionAttribute
  Specifies that this varible will switch type with their DTO type counterpart indirectly

  Should be used to convert IEnumerable<TModel> types with their IEnumerable<TDto> counterpart
  

  **converterType** Type name which has the public static methods that should convert the member atached to this attribute

  **methodName** The name of method that will convert the member original type to the *convertedType* and vice versa

  **convertedType** The type's fully qualified name that the member will be converted to and from

### Disclaimer
Besides being my first, it is my first public library, so feel free to make a issue or pull request to improve it;

Also, the license is a modified version from [here](https://github.com/non-ai-licenses/non-ai-licenses/blob/main/NON-AI-MIT)