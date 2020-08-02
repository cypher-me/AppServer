import React from "react";
import { connect } from "react-redux";
import { withTranslation } from 'react-i18next';
import { FieldContainer, Text, ComboBox, Loader, Button, toastr, Link } from "asc-web-components";
import styled from 'styled-components';
import { Trans } from 'react-i18next';
import { store, utils } from 'asc-web-common';
import { setLanguageAndTime, getPortalTimezones } from '../../../../../store/settings/actions';
import { default as clientStore } from '../../../../../store/store';
import SaveSettingsButtons from '../../../../save-settings-buttons';

const { changeLanguage } = utils;
const { getPortalCultures, getModules, getCurrentCustomSchema } = store.auth.actions;

const mapCulturesToArray = (cultures, t) => {
   return cultures.map((culture) => {
      return { key: culture, label: t(`Culture_${culture}`) };
   });
};

const mapTimezonesToArray = (timezones) => {
   return timezones.map((timezone) => {
      return { key: timezone.id, label: timezone.displayName };
   });
};

const findSelectedItemByKey = (items, selectedItemKey) => {
   return items.find(item => item.key === selectedItemKey);
}

const StyledComponent = styled.div`
   .margin-top {
      margin-top: 20px;
   }

   .margin-left {
      margin-left: 20px;
   }

   .settings-block {
      margin-bottom: 70px;
   }

   .field-container-width {
      max-width: 500px;
   }

   .combo-button-label {
      max-width: 100%;
   }
`;

const saveToSessionStorage = (key, value) => {
   sessionStorage.setItem(key, JSON.stringify(value))
}

const getFromSessionStorage = (key) => {
   return JSON.parse(sessionStorage.getItem(key));
}
let languageFromStorage = "";
let timezoneFromStorage = "";

const settingsOptions = [
   "language",
   "timezone"
]

class LanguageAndTimeZone extends React.Component {

   constructor(props) {
      super(props);

      const { portalLanguage, portalTimeZoneId, rawCultures, rawTimezones, t } = props;
      const languages = mapCulturesToArray(rawCultures, t);
      const timezones = mapTimezonesToArray(rawTimezones);
      const language = findSelectedItemByKey(languages, portalLanguage || languages[0]);
      const timezone = findSelectedItemByKey(timezones, portalTimeZoneId || timezones[0]);

      languageFromStorage = getFromSessionStorage("language");
      timezoneFromStorage = getFromSessionStorage("timezone");

      document.title = `${t("Customization")} – ${t("OrganizationName")}`;

      this.state = {
         isLoadedData: false,
         isLoading: false,
         timezones,
         timezone: timezoneFromStorage || timezone,
         timezoneDefault: timezone,
         languages,
         language: languageFromStorage || language,
         languageDefault: language,
         isLoadingGreetingSave: false,
         isLoadingGreetingRestore: false,
         hasChanged: false,
         showReminder: false
      }
   }

   componentDidMount() {
      const { getPortalCultures, portalLanguage, portalTimeZoneId, t, getPortalTimezones } = this.props;
      const { timezones, languages, isLoadedData, timezoneDefault, languageDefault, showReminder } = this.state;

      if((languageFromStorage || timezoneFromStorage) && !showReminder){
         this.setState({
            showReminder: true
         })
      }

      if (!timezones.length && !languages.length) {
         let languages;
         getPortalCultures()
            .then(() => {
               languages = mapCulturesToArray(this.props.rawCultures, t);
            })
            .then(() => getPortalTimezones())
            .then(() => {

               const timezones = mapTimezonesToArray(this.props.rawTimezones);
               
               const language = languageFromStorage || findSelectedItemByKey(languages, portalLanguage) || languages[0];
               const timezone = timezoneFromStorage || findSelectedItemByKey(timezones, portalTimeZoneId) || timezones[0];

               const languageDefault = findSelectedItemByKey(languages, portalLanguage) || languages[0];
               const timezoneDefault = findSelectedItemByKey(timezones, portalTimeZoneId) || timezones[0]

               this.setState({ languages, language, timezones, timezone, languageDefault, timezoneDefault });

               if(!timezoneDefault){
                  this.setState({
                     timezoneDefault: timezone
                  })
               }
               if(!languageDefault){
                  this.setState({
                     languageDefault: language
                  })
               }
            });
      }
      if (timezones.length && languages.length && !isLoadedData) {
         this.setState({ isLoadedData: true });
      }
   }

   componentDidUpdate(prevProps, prevState) {
      const { timezones, languages, timezoneDefault, languageDefault} = this.state;
      const { i18n, language, nameSchemaId } = this.props;
      if (timezones.length && languages.length && !prevState.isLoadedData) {
         this.setState({ isLoadedData: true });
      }
      if (language !== prevProps.language) {
         changeLanguage(i18n)
            .then((t) => {
               const newLocaleLanguages = mapCulturesToArray(this.props.rawCultures, t);
               const newLocaleSelectedLanguage = findSelectedItemByKey(newLocaleLanguages, this.state.language.key) || newLocaleLanguages[0];
               this.setState({
                  languages: newLocaleLanguages,
                  language: languageFromStorage || newLocaleSelectedLanguage
               });
            })
            .then(() => getModules(clientStore.dispatch))
            .then(() => getCurrentCustomSchema(clientStore.dispatch, nameSchemaId));
      }
      if(timezoneDefault && languageDefault){
         this.checkChanges()
      }
   }

   onLanguageSelect = (language) => {
      this.setState({ language })
      if(this.isEqualDefault("language", language)){
         saveToSessionStorage("language", "")
      } else {
         saveToSessionStorage("language", language)
      }
      this.checkChanges()
   };

   onTimezoneSelect = (timezone) => {
      this.setState({ timezone })
      if(this.isEqualDefault("timezone", timezone)){
         saveToSessionStorage("timezone", "")
      } else {
         saveToSessionStorage("timezone", timezone)
      }
      
      this.checkChanges()
   };

   onSaveLngTZSettings = () => {
      const { setLanguageAndTime, i18n } = this.props;
      this.setState({ isLoading: true }, function () {
         setLanguageAndTime(this.state.language.key, this.state.timezone.key)
            .then(() => changeLanguage(i18n))
            .then((t) => toastr.success(t("SuccessfullySaveSettingsMessage")))
            .catch((error) => toastr.error(error))
            .finally(() => this.setState({ isLoading: false }));
      })
   }

   onCancelClick = () => {

      for (let i = 0; i < settingsOptions.length; i++) {
         const value = getFromSessionStorage(settingsOptions[i]);

         if(value && !this.isEqualDefault(settingsOptions[i], value)) {
            const defaultValue = this.state[settingsOptions[i] + "Default"];

            this.setState({ [settingsOptions[i]]: defaultValue })
            saveToSessionStorage(settingsOptions[i], "")
         }
      }
      this.setState({
         showReminder: false
      })
      this.checkChanges()
   }

   isEqualDefault = (stateName, value) => {
      const defaultValue = JSON.stringify(this.state[stateName + "Default"])
      const currentValue = JSON.stringify(value)
      return defaultValue === currentValue
   }

   checkChanges = () => {
      let hasChanged = false;

      for (let i = 0; i < settingsOptions.length; i++) {
         const value = getFromSessionStorage(settingsOptions[i]);

         if(value && !this.isEqualDefault(settingsOptions[i], value)) hasChanged = true
      }

      if(hasChanged !== this.state.hasChanged){
         this.setState({
            hasChanged: hasChanged,
         })
      }
   }

   render() {
      const { t, i18n } = this.props;
      const { isLoadedData, languages, language, isLoading, timezones, timezone, hasChanged, showReminder } = this.state;
      const supportEmail = "documentation@onlyoffice.com";
      const tooltipLanguage =
         <Text fontSize='13px'>
            <Trans i18nKey="NotFoundLanguage" i18n={i18n}>
               "In case you cannot find your language in the list of the
               available ones, feel free to write to us at
               <Link href={`mailto:${supportEmail}`} isHovered={true}>
                  {{ supportEmail }}
               </Link> to take part in the translation and get up to 1 year free of
               charge."
            </Trans>
            {" "}
            <Link isHovered={true} href="https://helpcenter.onlyoffice.com/ru/guides/become-translator.aspx">{t("LearnMore")}</Link>
         </Text>

      return (
         !isLoadedData ?
            <Loader className="pageLoader" type="rombs" size='40px' />
            : <>
               <StyledComponent>
                  <div className='settings-block'>
                     <FieldContainer
                        id='fieldContainerLanguage'
                        className='field-container-width'
                        labelText={`${t("Language")}:`}
                        tooltipContent={tooltipLanguage}
                        helpButtonHeaderContent={t("Language")}
                        isVertical={true}>
                        <ComboBox
                           id='comboBoxLanguage'
                           options={languages}
                           selectedOption={language}
                           onSelect={this.onLanguageSelect}
                           isDisabled={isLoading}
                           noBorder={false}
                           scaled={true}
                           scaledOptions={true}
                           dropDownMaxHeight={300}
                           className='dropdown-item-width'
                        />
                     </FieldContainer>

                     <FieldContainer
                        id='fieldContainerTimezone'
                        className='field-container-width'
                        labelText={`${t("TimeZone")}:`}
                        isVertical={true}>
                        <ComboBox
                           id='comboBoxTimezone'
                           options={timezones}
                           selectedOption={timezone}
                           onSelect={this.onTimezoneSelect}
                           isDisabled={isLoading}
                           noBorder={false}
                           scaled={true}
                           scaledOptions={true}
                           dropDownMaxHeight={300}
                           className='dropdown-item-width'
                        />
                     </FieldContainer>
                  </div>
                  {hasChanged && 
                     <SaveSettingsButtons
                        onSaveClick={this.onSaveLngTZSettings}
                        onCancelClick={this.onCancelClick}
                        showReminder={showReminder}
                     />
                  }
                  
               </StyledComponent>
            </>
      );
   }
};

function mapStateToProps(state) {
   return {
      portalLanguage: state.auth.settings.culture,
      portalTimeZoneId: state.auth.settings.timezone,
      language: state.auth.user.cultureName || state.auth.settings.culture,
      rawTimezones: state.auth.settings.timezones,
      rawCultures: state.auth.settings.cultures,
      greetingSettings: state.auth.settings.greetingSettings,
      nameSchemaId: state.auth.settings.nameSchemaId
   };
}

export default connect(mapStateToProps, { getPortalCultures, setLanguageAndTime, getPortalTimezones})(withTranslation()(LanguageAndTimeZone));
